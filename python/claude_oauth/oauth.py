"""OAuth flow handling for Claude API authentication."""

from __future__ import annotations

import time
from dataclasses import dataclass
from typing import TYPE_CHECKING
from urllib.parse import urlencode

import httpx

from .constants import (
    CLAUDE_OAUTH_AUTHORIZE_URL,
    CLAUDE_OAUTH_CLIENT_ID,
    CLAUDE_OAUTH_REDIRECT_URI,
    CLAUDE_OAUTH_SCOPES,
    CLAUDE_OAUTH_TOKEN_URL,
)
from .pkce import PKCEPair, generate_pkce
from .storage import ClaudeOAuthCredentials, load_credentials, save_credentials

if TYPE_CHECKING:
    pass


@dataclass
class OAuthFlowResult:
    auth_url: str
    code_verifier: str


_pending_pkce: PKCEPair | None = None


def start_oauth_flow() -> OAuthFlowResult:
    global _pending_pkce
    pkce = generate_pkce()
    _pending_pkce = pkce

    params = {
        "code": "true",
        "client_id": CLAUDE_OAUTH_CLIENT_ID,
        "response_type": "code",
        "redirect_uri": CLAUDE_OAUTH_REDIRECT_URI,
        "scope": CLAUDE_OAUTH_SCOPES,
        "code_challenge": pkce.challenge,
        "code_challenge_method": "S256",
        "state": pkce.verifier,
    }
    auth_url = f"{CLAUDE_OAUTH_AUTHORIZE_URL}?{urlencode(params)}"
    return OAuthFlowResult(auth_url=auth_url, code_verifier=pkce.verifier)


def exchange_code_for_tokens(
    authorization_code: str, code_verifier: str | None = None
) -> ClaudeOAuthCredentials:
    global _pending_pkce

    verifier = code_verifier or (_pending_pkce.verifier if _pending_pkce else None)
    if not verifier:
        raise ValueError("No code verifier found. Start OAuth flow first.")

    code, state = authorization_code.strip().split("#", 1) if "#" in authorization_code else (authorization_code.strip(), "")

    with httpx.Client() as client:
        response = client.post(
            CLAUDE_OAUTH_TOKEN_URL,
            json={
                "code": code,
                "state": state,
                "grant_type": "authorization_code",
                "client_id": CLAUDE_OAUTH_CLIENT_ID,
                "redirect_uri": CLAUDE_OAUTH_REDIRECT_URI,
                "code_verifier": verifier,
            },
        )

    if not response.is_success:
        raise RuntimeError(f"Token exchange failed: {response.text}")

    data = response.json()
    _pending_pkce = None

    credentials = ClaudeOAuthCredentials(
        access_token=data["access_token"],
        refresh_token=data["refresh_token"],
        expires_at=_current_time_ms() + data["expires_in"] * 1000,
        connected_at=_current_time_ms(),
    )

    save_credentials(credentials)
    return credentials


def refresh_access_token(credentials: ClaudeOAuthCredentials) -> ClaudeOAuthCredentials:
    with httpx.Client() as client:
        response = client.post(
            CLAUDE_OAUTH_TOKEN_URL,
            json={
                "grant_type": "refresh_token",
                "refresh_token": credentials.refresh_token,
                "client_id": CLAUDE_OAUTH_CLIENT_ID,
            },
        )

    if not response.is_success:
        raise RuntimeError(f"Token refresh failed: {response.text}")

    data = response.json()

    new_credentials = ClaudeOAuthCredentials(
        access_token=data["access_token"],
        refresh_token=data.get("refresh_token", credentials.refresh_token),
        expires_at=_current_time_ms() + data["expires_in"] * 1000,
        connected_at=credentials.connected_at,
    )

    save_credentials(new_credentials)
    return new_credentials


def get_valid_access_token() -> str:
    credentials = load_credentials()
    if not credentials:
        raise RuntimeError("Not authenticated. Run login first.")

    buffer_ms = 5 * 60 * 1000
    if credentials.expires_at <= _current_time_ms() + buffer_ms:
        credentials = refresh_access_token(credentials)

    return credentials.access_token


def _current_time_ms() -> int:
    return int(time.time() * 1000)

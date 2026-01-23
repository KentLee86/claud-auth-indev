"""PKCE (Proof Key for Code Exchange) utilities for secure OAuth flow without client secrets."""

import base64
import hashlib
import secrets
from dataclasses import dataclass


@dataclass
class PKCEPair:
    verifier: str
    challenge: str


def generate_code_verifier() -> str:
    random_bytes = secrets.token_bytes(64)
    return base64.urlsafe_b64encode(random_bytes).decode("ascii").rstrip("=")


def generate_code_challenge(verifier: str) -> str:
    digest = hashlib.sha256(verifier.encode("ascii")).digest()
    return base64.urlsafe_b64encode(digest).decode("ascii").rstrip("=")


def generate_pkce() -> PKCEPair:
    verifier = generate_code_verifier()
    challenge = generate_code_challenge(verifier)
    return PKCEPair(verifier=verifier, challenge=challenge)

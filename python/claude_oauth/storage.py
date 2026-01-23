"""Token storage utilities - stores OAuth credentials securely in user's home directory."""

from __future__ import annotations

import json
import os
import stat
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    pass


@dataclass
class ClaudeOAuthCredentials:
    access_token: str
    refresh_token: str
    expires_at: int
    connected_at: int


CONFIG_DIR = Path.home() / ".claude-oauth"
CREDENTIALS_FILE = CONFIG_DIR / "credentials.json"


def _ensure_config_dir() -> None:
    CONFIG_DIR.mkdir(parents=True, exist_ok=True)


def save_credentials(credentials: ClaudeOAuthCredentials) -> None:
    _ensure_config_dir()
    data = {
        "accessToken": credentials.access_token,
        "refreshToken": credentials.refresh_token,
        "expiresAt": credentials.expires_at,
        "connectedAt": credentials.connected_at,
    }
    CREDENTIALS_FILE.write_text(json.dumps(data, indent=2))
    if os.name != "nt":
        CREDENTIALS_FILE.chmod(stat.S_IRUSR | stat.S_IWUSR)


def load_credentials() -> ClaudeOAuthCredentials | None:
    try:
        if not CREDENTIALS_FILE.exists():
            return None
        data = json.loads(CREDENTIALS_FILE.read_text())
        return ClaudeOAuthCredentials(
            access_token=data["accessToken"],
            refresh_token=data["refreshToken"],
            expires_at=data["expiresAt"],
            connected_at=data["connectedAt"],
        )
    except (json.JSONDecodeError, KeyError, OSError):
        return None


def clear_credentials() -> None:
    try:
        CREDENTIALS_FILE.unlink()
    except FileNotFoundError:
        pass


def has_valid_credentials() -> bool:
    credentials = load_credentials()
    if not credentials:
        return False
    buffer_ms = 5 * 60 * 1000
    return credentials.expires_at > _current_time_ms() + buffer_ms


def get_config_dir() -> Path:
    return CONFIG_DIR


def _current_time_ms() -> int:
    import time
    return int(time.time() * 1000)

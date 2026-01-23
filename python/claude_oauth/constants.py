"""
Claude OAuth constants
These are public values used by Claude Code and compatible third-party apps
"""

# OAuth client ID (same as Claude Code / opencode)
CLAUDE_OAUTH_CLIENT_ID = "9d1c250a-e61b-44d9-88ed-5944d1962f5e"

# OAuth endpoints
CLAUDE_OAUTH_AUTHORIZE_URL = "https://claude.ai/oauth/authorize"
CLAUDE_OAUTH_TOKEN_URL = "https://console.anthropic.com/v1/oauth/token"
CLAUDE_OAUTH_REDIRECT_URI = "https://console.anthropic.com/oauth/code/callback"

# OAuth scopes
CLAUDE_OAUTH_SCOPES = "org:create_api_key user:profile user:inference"

# API endpoints
ANTHROPIC_API_URL = "https://api.anthropic.com/v1"

# Default callback port for local server
DEFAULT_CALLBACK_PORT = 54545

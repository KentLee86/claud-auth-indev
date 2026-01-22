/**
 * Claude OAuth constants
 * These are public values used by Claude Code and compatible third-party apps
 */

// OAuth client ID (same as Claude Code / opencode)
export const CLAUDE_OAUTH_CLIENT_ID = '9d1c250a-e61b-44d9-88ed-5944d1962f5e'

// OAuth endpoints
export const CLAUDE_OAUTH_AUTHORIZE_URL = 'https://claude.ai/oauth/authorize'
export const CLAUDE_OAUTH_TOKEN_URL = 'https://console.anthropic.com/v1/oauth/token'
export const CLAUDE_OAUTH_REDIRECT_URI = 'https://console.anthropic.com/oauth/code/callback'

// OAuth scopes
export const CLAUDE_OAUTH_SCOPES = 'org:create_api_key user:profile user:inference'

// API endpoints
export const ANTHROPIC_API_URL = 'https://api.anthropic.com/v1'

// Default callback port for local server
export const DEFAULT_CALLBACK_PORT = 54545

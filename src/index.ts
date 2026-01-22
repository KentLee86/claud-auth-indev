export {
  CLAUDE_OAUTH_CLIENT_ID,
  CLAUDE_OAUTH_AUTHORIZE_URL,
  CLAUDE_OAUTH_TOKEN_URL,
  CLAUDE_OAUTH_REDIRECT_URI,
  CLAUDE_OAUTH_SCOPES,
  ANTHROPIC_API_URL,
} from './lib/constants'

export { generatePKCE, generateCodeVerifier, generateCodeChallenge } from './lib/pkce'
export type { PKCEPair } from './lib/pkce'

export {
  saveCredentials,
  loadCredentials,
  clearCredentials,
  hasValidCredentials,
  getConfigDir,
} from './lib/storage'
export type { ClaudeOAuthCredentials } from './lib/storage'

export {
  startOAuthFlow,
  exchangeCodeForTokens,
  refreshAccessToken,
  getValidAccessToken,
  startLocalCallbackServer,
} from './lib/oauth'
export type { OAuthFlowResult } from './lib/oauth'

export { chat, chatStream, ask } from './lib/client'
export type { Message, ChatOptions, ChatResponse } from './lib/client'

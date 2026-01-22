import {
  CLAUDE_OAUTH_CLIENT_ID,
  CLAUDE_OAUTH_AUTHORIZE_URL,
  CLAUDE_OAUTH_TOKEN_URL,
  CLAUDE_OAUTH_REDIRECT_URI,
  CLAUDE_OAUTH_SCOPES,
  DEFAULT_CALLBACK_PORT,
} from './constants'
import { generatePKCE, type PKCEPair } from './pkce'
import { saveCredentials, loadCredentials, type ClaudeOAuthCredentials } from './storage'

interface TokenResponse {
  access_token: string
  refresh_token: string
  expires_in: number
  token_type: string
}

let pendingPKCE: PKCEPair | null = null

export interface OAuthFlowResult {
  authUrl: string
  codeVerifier: string
}

export function startOAuthFlow(): OAuthFlowResult {
  const pkce = generatePKCE()
  pendingPKCE = pkce

  const url = new URL(CLAUDE_OAUTH_AUTHORIZE_URL)
  url.searchParams.set('code', 'true')
  url.searchParams.set('client_id', CLAUDE_OAUTH_CLIENT_ID)
  url.searchParams.set('response_type', 'code')
  url.searchParams.set('redirect_uri', CLAUDE_OAUTH_REDIRECT_URI)
  url.searchParams.set('scope', CLAUDE_OAUTH_SCOPES)
  url.searchParams.set('code_challenge', pkce.challenge)
  url.searchParams.set('code_challenge_method', 'S256')
  url.searchParams.set('state', pkce.verifier)

  return {
    authUrl: url.toString(),
    codeVerifier: pkce.verifier,
  }
}

export async function exchangeCodeForTokens(
  authorizationCode: string,
  codeVerifier?: string
): Promise<ClaudeOAuthCredentials> {
  const verifier = codeVerifier ?? pendingPKCE?.verifier
  if (!verifier) {
    throw new Error('No code verifier found. Start OAuth flow first.')
  }

  const [code, state] = authorizationCode.trim().split('#')

  const response = await fetch(CLAUDE_OAUTH_TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      code,
      state,
      grant_type: 'authorization_code',
      client_id: CLAUDE_OAUTH_CLIENT_ID,
      redirect_uri: CLAUDE_OAUTH_REDIRECT_URI,
      code_verifier: verifier,
    }),
  })

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(`Token exchange failed: ${errorText}`)
  }

  const data = (await response.json()) as TokenResponse
  pendingPKCE = null

  const credentials: ClaudeOAuthCredentials = {
    accessToken: data.access_token,
    refreshToken: data.refresh_token,
    expiresAt: Date.now() + data.expires_in * 1000,
    connectedAt: Date.now(),
  }

  await saveCredentials(credentials)
  return credentials
}

export async function refreshAccessToken(
  credentials: ClaudeOAuthCredentials
): Promise<ClaudeOAuthCredentials> {
  const response = await fetch(CLAUDE_OAUTH_TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      grant_type: 'refresh_token',
      refresh_token: credentials.refreshToken,
      client_id: CLAUDE_OAUTH_CLIENT_ID,
    }),
  })

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(`Token refresh failed: ${errorText}`)
  }

  const data = (await response.json()) as TokenResponse

  const newCredentials: ClaudeOAuthCredentials = {
    accessToken: data.access_token,
    refreshToken: data.refresh_token ?? credentials.refreshToken,
    expiresAt: Date.now() + data.expires_in * 1000,
    connectedAt: credentials.connectedAt,
  }

  await saveCredentials(newCredentials)
  return newCredentials
}

export async function getValidAccessToken(): Promise<string> {
  let credentials = await loadCredentials()
  if (!credentials) {
    throw new Error('Not authenticated. Run login first.')
  }

  const bufferMs = 5 * 60 * 1000
  if (credentials.expiresAt <= Date.now() + bufferMs) {
    credentials = await refreshAccessToken(credentials)
  }

  return credentials.accessToken
}

export async function startLocalCallbackServer(
  port: number = DEFAULT_CALLBACK_PORT
): Promise<string> {
  return new Promise((resolve, reject) => {
    const server = Bun.serve({
      port,
      fetch(req) {
        const url = new URL(req.url)
        if (url.pathname === '/callback') {
          const code = url.searchParams.get('code')
          const error = url.searchParams.get('error')

          if (error) {
            reject(new Error(`OAuth error: ${error}`))
            setTimeout(() => server.stop(), 100)
            return new Response('Authentication failed. You can close this window.', {
              headers: { 'Content-Type': 'text/html' },
            })
          }

          if (code) {
            resolve(code)
            setTimeout(() => server.stop(), 100)
            return new Response(
              '<html><body><h1>Success!</h1><p>You can close this window.</p></body></html>',
              { headers: { 'Content-Type': 'text/html' } }
            )
          }
        }

        return new Response('Not found', { status: 404 })
      },
    })

    setTimeout(() => {
      server.stop()
      reject(new Error('OAuth callback timeout (5 minutes)'))
    }, 5 * 60 * 1000)
  })
}

/**
 * PKCE (Proof Key for Code Exchange) utilities
 * Used for secure OAuth flow without client secrets
 */

import crypto from 'crypto'

export interface PKCEPair {
  verifier: string
  challenge: string
}

/**
 * Generate a random code verifier for PKCE
 * @returns Base64url encoded random string
 */
export function generateCodeVerifier(): string {
  const buffer = crypto.randomBytes(64)
  return buffer
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=/g, '')
}

/**
 * Generate a code challenge from a verifier using SHA256
 * @param verifier - The code verifier string
 * @returns Base64url encoded SHA256 hash
 */
export function generateCodeChallenge(verifier: string): string {
  const hash = crypto.createHash('sha256').update(verifier).digest()
  return hash
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=/g, '')
}

/**
 * Generate a complete PKCE pair (verifier and challenge)
 * @returns Object containing verifier and challenge
 */
export function generatePKCE(): PKCEPair {
  const verifier = generateCodeVerifier()
  const challenge = generateCodeChallenge(verifier)
  return { verifier, challenge }
}

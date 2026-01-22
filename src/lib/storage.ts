/**
 * Token storage utilities
 * Stores OAuth credentials securely in user's home directory
 */

import os from 'os'
import path from 'path'

export interface ClaudeOAuthCredentials {
  accessToken: string
  refreshToken: string
  expiresAt: number
  connectedAt: number
}

const CONFIG_DIR = path.join(os.homedir(), '.claude-oauth')
const CREDENTIALS_FILE = path.join(CONFIG_DIR, 'credentials.json')

/**
 * Ensure config directory exists
 */
async function ensureConfigDir(): Promise<void> {
  const dir = Bun.file(CONFIG_DIR)
  if (!(await dir.exists())) {
    await Bun.write(path.join(CONFIG_DIR, '.keep'), '')
  }
}

/**
 * Save OAuth credentials to file
 */
export async function saveCredentials(credentials: ClaudeOAuthCredentials): Promise<void> {
  await ensureConfigDir()
  await Bun.write(CREDENTIALS_FILE, JSON.stringify(credentials, null, 2))
  // Set restrictive permissions (owner read/write only)
  if (process.platform !== 'win32') {
    const { chmod } = await import('fs/promises')
    await chmod(CREDENTIALS_FILE, 0o600)
  }
}

/**
 * Load OAuth credentials from file
 * @returns Credentials or null if not found
 */
export async function loadCredentials(): Promise<ClaudeOAuthCredentials | null> {
  try {
    const file = Bun.file(CREDENTIALS_FILE)
    if (!(await file.exists())) {
      return null
    }
    const content = await file.text()
    return JSON.parse(content) as ClaudeOAuthCredentials
  } catch {
    return null
  }
}

/**
 * Clear stored credentials
 */
export async function clearCredentials(): Promise<void> {
  try {
    const { unlink } = await import('fs/promises')
    await unlink(CREDENTIALS_FILE)
  } catch {
    // Ignore if file doesn't exist
  }
}

/**
 * Check if credentials exist and are valid (not expired)
 */
export async function hasValidCredentials(): Promise<boolean> {
  const credentials = await loadCredentials()
  if (!credentials) return false
  
  // Check if access token is expired (with 5 minute buffer)
  const bufferMs = 5 * 60 * 1000
  return credentials.expiresAt > Date.now() + bufferMs
}

/**
 * Get config directory path
 */
export function getConfigDir(): string {
  return CONFIG_DIR
}

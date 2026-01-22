# AGENTS.md - Claude OAuth Client

This document provides guidelines for AI coding agents working in this repository.

## Project Overview

A Bun-based TypeScript OAuth client for the Claude API. Uses the same authentication flow as Claude Code / opencode. Provides both CLI and library interfaces for interacting with Claude.

## Build Commands

```bash
# Install dependencies
bun install

# Run CLI
bun run src/cli.ts [command]

# Run tests
bun test

# Run a single test file
bun test path/to/file.test.ts

# Run tests matching a pattern
bun test --grep "pattern"

# Type check (no emit, bundler mode)
bunx tsc --noEmit
```

## Available Scripts

```bash
bun run dev      # Run CLI in dev mode
bun run login    # Start OAuth authentication
bun run status   # Check auth status
bun run chat     # Interactive chat session
bun run test     # Run all tests
```

## Project Structure

```
src/
├── index.ts           # Public API exports
├── cli.ts             # CLI entry point
└── lib/
    ├── client.ts      # Claude API client (chat, chatStream, ask)
    ├── oauth.ts       # OAuth flow handling
    ├── pkce.ts        # PKCE utilities for secure auth
    ├── storage.ts     # Credential storage (~/.claude-oauth/)
    └── constants.ts   # OAuth/API constants
```

## Code Style Guidelines

### Runtime & Package Manager

- **ALWAYS use Bun**, never Node.js
- Use `bun` for running files: `bun <file>` not `node <file>`
- Use `bun install` not `npm/yarn/pnpm install`
- Use `bunx` not `npx`
- Bun auto-loads `.env` - never use `dotenv`

### Bun-Native APIs (REQUIRED)

Prefer Bun's built-in APIs over external packages:

| Task | Use | Don't Use |
|------|-----|-----------|
| HTTP Server | `Bun.serve()` | express, fastify |
| SQLite | `bun:sqlite` | better-sqlite3 |
| Redis | `Bun.redis` | ioredis |
| Postgres | `Bun.sql` | pg, postgres.js |
| WebSocket | Built-in `WebSocket` | ws |
| File I/O | `Bun.file` | fs.readFile/writeFile |
| Shell | `Bun.$\`cmd\`` | execa |
| Testing | `bun:test` | jest, vitest |

### TypeScript Configuration

Strict mode enabled with these key settings:
- `strict: true`
- `noUncheckedIndexedAccess: true` - Array access may be undefined
- `noFallthroughCasesInSwitch: true`
- `noImplicitOverride: true`
- `verbatimModuleSyntax: true` - Use explicit `type` imports

### Import Style

```typescript
// Named exports preferred
export { functionName } from './module'
export type { TypeName } from './module'

// Type-only imports for interfaces/types
import type { SomeType } from './types'

// Node built-ins with node: prefix
import os from 'os'
import path from 'path'
import crypto from 'crypto'
```

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Functions | camelCase | `startOAuthFlow` |
| Interfaces | PascalCase | `ChatOptions` |
| Types | PascalCase | `PKCEPair` |
| Constants | SCREAMING_SNAKE | `CLAUDE_OAUTH_CLIENT_ID` |
| Files | kebab-case or camelCase | `oauth.ts`, `storage.ts` |
| Directories | kebab-case | `lib/` |

### Type Definitions

```typescript
// Interface for object shapes
export interface ChatOptions {
  model?: string
  maxTokens?: number
  system?: string
}

// Response types with camelCase properties (transform from snake_case API)
export interface ChatResponse {
  id: string
  content: string
  stopReason: string  // Transformed from stop_reason
  usage: {
    inputTokens: number   // Transformed from input_tokens
    outputTokens: number  // Transformed from output_tokens
  }
}
```

### Error Handling

```typescript
// Throw descriptive Error instances
if (!response.ok) {
  const errorText = await response.text()
  throw new Error(`API request failed: ${response.status} - ${errorText}`)
}

// Use try-catch with silent fallbacks where appropriate
try {
  const content = await file.text()
  return JSON.parse(content)
} catch {
  return null  // Silent fallback for optional operations
}
```

### Async Patterns

```typescript
// Async/await for promises
export async function loadCredentials(): Promise<Credentials | null> {
  // ...
}

// AsyncGenerator for streaming
export async function* chatStream(
  messages: Message[]
): AsyncGenerator<string, ChatResponse> {
  // yield chunks, return final response
}
```

### API Request Pattern

```typescript
const response = await fetch(url, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${accessToken}`,
    'anthropic-version': '2023-06-01',
  },
  body: JSON.stringify(payload),
})

if (!response.ok) {
  const errorText = await response.text()
  throw new Error(`Request failed: ${response.status} - ${errorText}`)
}

const data = (await response.json()) as ExpectedType
```

### File Operations

```typescript
// Reading files
const file = Bun.file(filePath)
if (await file.exists()) {
  const content = await file.text()
}

// Writing files
await Bun.write(filePath, JSON.stringify(data, null, 2))

// File permissions (non-Windows)
if (process.platform !== 'win32') {
  const { chmod } = await import('fs/promises')
  await chmod(filePath, 0o600)
}
```

### Testing

```typescript
import { test, expect, describe } from 'bun:test'

describe('module', () => {
  test('should do something', () => {
    expect(result).toBe(expected)
  })
})
```

## Key Architecture Decisions

1. **PKCE Flow**: Uses S256 code challenge method for secure OAuth without client secrets
2. **Token Storage**: Credentials stored in `~/.claude-oauth/credentials.json` with 600 permissions
3. **Auto-refresh**: Tokens refreshed automatically with 5-minute buffer before expiry
4. **Streaming**: Uses Server-Sent Events (SSE) for real-time chat responses
5. **Module System**: ESM-only (`"type": "module"` in package.json)

## Common Patterns

### Adding New API Functions

1. Add to `src/lib/client.ts`
2. Export from `src/index.ts`
3. Add CLI command in `src/cli.ts` if needed

### Modifying OAuth Flow

1. Constants in `src/lib/constants.ts`
2. Flow logic in `src/lib/oauth.ts`
3. Storage in `src/lib/storage.ts`

## Things to Avoid

- Don't use `as any` or `@ts-ignore`
- Don't suppress type errors
- Don't use Node.js alternatives when Bun provides built-in functionality
- Don't commit credentials or sensitive data
- Don't use CommonJS syntax (`require`, `module.exports`)

# Claude OAuth Client

Personal Claude OAuth library using the same authentication flow as Claude Code / opencode.

## Setup

```bash
bun install
```

## Usage

### CLI

```bash
# Authenticate (get OAuth token)
bun run login

# Check authentication status
bun run status

# Interactive chat
bun run chat

# Single question
bun run src/cli.ts ask "What is the meaning of life?"

# Clear credentials
bun run src/cli.ts logout
```

### Library

```typescript
import {
  startOAuthFlow,
  exchangeCodeForTokens,
  chat,
  chatStream,
  ask,
  hasValidCredentials,
} from './src'

// Check if already authenticated
if (await hasValidCredentials()) {
  // Simple one-shot question
  const answer = await ask('Hello, how are you?')
  console.log(answer)

  // Chat with message history
  const response = await chat([
    { role: 'user', content: 'What is 2+2?' },
    { role: 'assistant', content: '4' },
    { role: 'user', content: 'And 3+3?' },
  ])
  console.log(response.content)

  // Streaming response
  for await (const chunk of chatStream([{ role: 'user', content: 'Tell me a story' }])) {
    process.stdout.write(chunk)
  }
}
```

### OAuth Flow

```typescript
import { startOAuthFlow, exchangeCodeForTokens } from './src'

// 1. Generate auth URL
const { authUrl, codeVerifier } = startOAuthFlow()
console.log('Open this URL:', authUrl)

// 2. User authorizes and gets code (format: CODE#STATE)
const authCode = 'received_code#state'

// 3. Exchange code for tokens
const credentials = await exchangeCodeForTokens(authCode, codeVerifier)
```

## API

### Functions

- `startOAuthFlow()` - Start OAuth PKCE flow, returns auth URL
- `exchangeCodeForTokens(code, verifier)` - Exchange auth code for tokens
- `refreshAccessToken(credentials)` - Refresh expired access token
- `getValidAccessToken()` - Get valid token (auto-refreshes if needed)
- `chat(messages, options)` - Send chat messages
- `chatStream(messages, options)` - Stream chat response
- `ask(prompt, options)` - Simple one-shot question
- `hasValidCredentials()` - Check if authenticated
- `loadCredentials()` - Load stored credentials
- `clearCredentials()` - Clear stored credentials

### Types

```typescript
interface Message {
  role: 'user' | 'assistant'
  content: string
}

interface ChatOptions {
  model?: string      // default: claude-sonnet-4-20250514
  maxTokens?: number  // default: 4096
  system?: string     // system prompt
}

interface ChatResponse {
  id: string
  content: string
  model: string
  stopReason: string
  usage: { inputTokens: number; outputTokens: number }
}
```

## Storage

Credentials are stored in `~/.claude-oauth/credentials.json` with 600 permissions.

---

## Python

### Installation

```bash
# From GitHub
pip install git+https://github.com/code-yeongyu/claude-oauth.git#subdirectory=python

# Local development
cd python
pip install -e .
```

### CLI

```bash
claude-oauth login                              # Authenticate
claude-oauth status                             # Check status
claude-oauth logout                             # Clear credentials
claude-oauth chat                               # Interactive chat
claude-oauth ask "What is 2+2?"                 # Single question
claude-oauth ask -m haiku "Quick question"      # With model option
claude-oauth ask -f image.png "What do you see?" # With file attachment
```

Options:
- `-m, --model {haiku,sonnet,opus}` : Model selection (default: sonnet)
- `-f, --file <path>` : Attach file (can use multiple times)

### Library

```python
from claude_oauth import ask, chat, chat_stream, has_valid_credentials, ChatOptions
import asyncio

# Simple question
answer = ask("Hello!")

# With model option
answer = ask("Hello!", ChatOptions(model="claude-haiku-4-5-20251001"))

# Streaming
async def run():
    async for chunk in chat_stream([{"role": "user", "content": "Tell a story"}]):
        print(chunk, end="")
asyncio.run(run())
```

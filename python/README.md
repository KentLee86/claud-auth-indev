# Claude OAuth Client (Python)

Python port of the Claude OAuth client library.

## Install

```bash
pip install -e .
```

## CLI

```bash
claude-oauth login     # Authenticate
claude-oauth status    # Check status
claude-oauth logout    # Clear credentials
claude-oauth chat      # Interactive chat
```

### ask

```bash
claude-oauth ask "What is 2+2?"
claude-oauth ask -m haiku "Quick question"
claude-oauth ask -m opus "Complex question"
claude-oauth ask -f image.png "What do you see?"
claude-oauth ask -m haiku -f a.png -f b.png "Compare these"
```

Options:
- `-m, --model {haiku,sonnet,opus}` : Model (default: sonnet)
- `-f, --file <path>` : Attach file (can use multiple times)

### chat

```bash
claude-oauth chat
```

Commands in chat:
- `/haiku`, `/sonnet`, `/opus` : Switch model
- `/file <path>` : Attach file
- `/clear` : Clear attached files
- `/help` : Show help
- `/exit` : Exit

### Image Compression

Images over 3.7MB are automatically compressed to stay under the 5MB API limit (base64 encoded).

## Library

```python
from claude_oauth import ask, chat, chat_stream, has_valid_credentials, ChatOptions
import asyncio

# Simple question
answer = ask("Hello!")

# With model option
answer = ask("Hello!", ChatOptions(model="claude-haiku-4-5-20251001"))

# Streaming
stream = chat_stream([{"role": "user", "content": "Tell a story"}])
async def run():
    async for chunk in stream:
        print(chunk, end="")
asyncio.run(run())
```

### Image Analysis

```python
import base64
from pathlib import Path
from claude_oauth import chat

image_data = base64.b64encode(Path("image.png").read_bytes()).decode()

response = chat([{
    "role": "user",
    "content": [
        {"type": "image", "source": {"type": "base64", "media_type": "image/png", "data": image_data}},
        {"type": "text", "text": "What do you see?"}
    ]
}])
print(response.content)
```

## Credentials

Stored in `~/.claude-oauth/credentials.json` (shared with TypeScript version).

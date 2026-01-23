# Claude OAuth Client (Python)

Python port of the Claude OAuth client library.

## Install

```bash
pip install -e .
```

## CLI

```bash
claude-oauth login    # Authenticate
claude-oauth status   # Check status
claude-oauth ask "What is 2+2?"
claude-oauth chat     # Interactive chat
claude-oauth logout   # Clear credentials
```

## Library

```python
from claude_oauth import ask, chat, chat_stream, has_valid_credentials
import asyncio

# Simple question
answer = ask("Hello!")

# Streaming
stream = chat_stream([{"role": "user", "content": "Tell a story"}])
async def run():
    async for chunk in stream:
        print(chunk, end="")
asyncio.run(run())
```

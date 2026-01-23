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

### Windows

Windows에서 `claude-oauth` 명령어가 작동하지 않는 경우, 모듈로 직접 실행하세요:

```bash
python -m claude_oauth.cli login    # 인증
python -m claude_oauth.cli status   # 상태 확인
python -m claude_oauth.cli ask "What is 2+2?"
python -m claude_oauth.cli chat     # 대화형 채팅
python -m claude_oauth.cli logout   # 인증 정보 삭제
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

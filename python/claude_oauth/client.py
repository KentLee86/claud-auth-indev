from __future__ import annotations

import json
from collections.abc import AsyncIterator
from dataclasses import dataclass, field
from typing import Literal, TypedDict

import httpx

from .constants import ANTHROPIC_API_URL
from .oauth import get_valid_access_token


class ImageSource(TypedDict):
    type: Literal["base64"]
    media_type: str
    data: str


class TextBlock(TypedDict):
    type: Literal["text"]
    text: str


class ImageBlock(TypedDict):
    type: Literal["image"]
    source: ImageSource


ContentBlock = TextBlock | ImageBlock


class Message(TypedDict):
    role: Literal["user", "assistant"]
    content: str | list[ContentBlock]


@dataclass
class ChatOptions:
    model: str | None = None
    max_tokens: int | None = None
    system: str | None = None


@dataclass
class ChatResponse:
    id: str
    content: str
    model: str
    stop_reason: str
    input_tokens: int
    output_tokens: int


DEFAULT_MODEL = "claude-opus-4-5"
DEFAULT_MAX_TOKENS = 4096
CLAUDE_CODE_SYSTEM_PREFIX = "You are Claude Code, Anthropic's official CLI for Claude."
ANTHROPIC_BETA_FLAGS = "oauth-2025-04-20,interleaved-thinking-2025-05-14"
USER_AGENT = "claude-cli/2.1.2 (external, cli)"


def chat(messages: list[Message], options: ChatOptions | None = None) -> ChatResponse:
    opts = options or ChatOptions()
    access_token = get_valid_access_token()

    system_prompt = (
        f"{CLAUDE_CODE_SYSTEM_PREFIX}\n\n{opts.system}"
        if opts.system
        else CLAUDE_CODE_SYSTEM_PREFIX
    )

    with httpx.Client(timeout=120.0) as client:
        response = client.post(
            f"{ANTHROPIC_API_URL}/messages?beta=true",
            headers={
                "Content-Type": "application/json",
                "Authorization": f"Bearer {access_token}",
                "anthropic-version": "2023-06-01",
                "anthropic-beta": ANTHROPIC_BETA_FLAGS,
                "user-agent": USER_AGENT,
            },
            json={
                "model": opts.model or DEFAULT_MODEL,
                "max_tokens": opts.max_tokens or DEFAULT_MAX_TOKENS,
                "system": system_prompt,
                "messages": [{"role": m["role"], "content": m["content"]} for m in messages],
            },
        )

    if not response.is_success:
        raise RuntimeError(f"API request failed: {response.status_code} - {response.text}")

    data = response.json()

    return ChatResponse(
        id=data["id"],
        content=data["content"][0].get("text", "") if data["content"] else "",
        model=data["model"],
        stop_reason=data["stop_reason"],
        input_tokens=data["usage"]["input_tokens"],
        output_tokens=data["usage"]["output_tokens"],
    )


@dataclass
class ChatStream:
    messages: list[Message]
    options: ChatOptions = field(default_factory=ChatOptions)
    response: ChatResponse | None = field(default=None, init=False)
    _full_content: str = field(default="", init=False)

    async def __aiter__(self) -> AsyncIterator[str]:
        opts = self.options
        access_token = get_valid_access_token()

        system_prompt = (
            f"{CLAUDE_CODE_SYSTEM_PREFIX}\n\n{opts.system}"
            if opts.system
            else CLAUDE_CODE_SYSTEM_PREFIX
        )

        final_data: dict[str, str | int] = {}

        async with httpx.AsyncClient(timeout=120.0) as client:
            async with client.stream(
                "POST",
                f"{ANTHROPIC_API_URL}/messages?beta=true",
                headers={
                    "Content-Type": "application/json",
                    "Authorization": f"Bearer {access_token}",
                    "anthropic-version": "2023-06-01",
                    "anthropic-beta": ANTHROPIC_BETA_FLAGS,
                    "user-agent": USER_AGENT,
                },
                json={
                    "model": opts.model or DEFAULT_MODEL,
                    "max_tokens": opts.max_tokens or DEFAULT_MAX_TOKENS,
                    "system": system_prompt,
                    "stream": True,
                    "messages": [{"role": m["role"], "content": m["content"]} for m in self.messages],
                },
            ) as http_response:
                if not http_response.is_success:
                    error_text = await http_response.aread()
                    raise RuntimeError(
                        f"API request failed: {http_response.status_code} - {error_text.decode()}"
                    )

                async for line in http_response.aiter_lines():
                    if not line.startswith("data: "):
                        continue

                    json_str = line[6:]
                    if json_str == "[DONE]":
                        continue

                    try:
                        event = json.loads(json_str)

                        if event.get("type") == "content_block_delta":
                            text = event.get("delta", {}).get("text", "")
                            if text:
                                self._full_content += text
                                yield text

                        if event.get("type") == "message_start" and event.get("message"):
                            final_data["id"] = event["message"]["id"]
                            final_data["model"] = event["message"]["model"]

                        if event.get("type") == "message_delta":
                            final_data["stop_reason"] = event.get("delta", {}).get("stop_reason", "")
                            if event.get("usage"):
                                final_data["input_tokens"] = event["usage"].get("input_tokens", 0)
                                final_data["output_tokens"] = event["usage"].get("output_tokens", 0)

                    except json.JSONDecodeError:
                        continue

        self.response = ChatResponse(
            id=str(final_data.get("id", "")),
            content=self._full_content,
            model=str(final_data.get("model", "")),
            stop_reason=str(final_data.get("stop_reason", "")),
            input_tokens=int(final_data.get("input_tokens", 0)),
            output_tokens=int(final_data.get("output_tokens", 0)),
        )


def chat_stream(messages: list[Message], options: ChatOptions | None = None) -> ChatStream:
    return ChatStream(messages=messages, options=options or ChatOptions())


def ask(prompt: str, options: ChatOptions | None = None) -> str:
    response = chat([{"role": "user", "content": prompt}], options)
    return response.content

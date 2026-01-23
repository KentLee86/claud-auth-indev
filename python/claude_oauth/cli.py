#!/usr/bin/env python3
from __future__ import annotations

import argparse
import asyncio
import base64
import io
import platform
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

from PIL import Image

from .client import ChatOptions, ChatStream, ContentBlock, Message, chat_stream
from .oauth import exchange_code_for_tokens, start_oauth_flow
from .storage import (
    clear_credentials,
    get_config_dir,
    has_valid_credentials,
    load_credentials,
)

MODELS = {
    "haiku": "claude-haiku-4-5-20251001",
    "sonnet": "claude-sonnet-4-20250514",
    "opus": "claude-opus-4-20250514",
}

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".gif", ".webp"}
MAX_IMAGE_SIZE_BASE64 = 5 * 1024 * 1024  # 5MB (API limit for base64)
MAX_IMAGE_SIZE_RAW = int(MAX_IMAGE_SIZE_BASE64 / 1.34)  # ~3.7MB (accounts for base64 overhead)


def get_media_type(ext: str) -> str:
    types = {
        ".png": "image/png",
        ".jpg": "image/jpeg",
        ".jpeg": "image/jpeg",
        ".gif": "image/gif",
        ".webp": "image/webp",
    }
    return types.get(ext, "application/octet-stream")


def compress_image(file_path: Path, max_size: int = MAX_IMAGE_SIZE_RAW) -> tuple[bytes, str]:
    img = Image.open(file_path)
    
    if img.mode in ("RGBA", "P"):
        img = img.convert("RGB")
    
    output_format = "JPEG"
    media_type = "image/jpeg"
    
    quality = 95
    while quality >= 20:
        buffer = io.BytesIO()
        img.save(buffer, format=output_format, quality=quality, optimize=True)
        data = buffer.getvalue()
        
        if len(data) <= max_size:
            return data, media_type
        
        quality -= 10
    
    while True:
        width, height = img.size
        new_width = int(width * 0.8)
        new_height = int(height * 0.8)
        
        if new_width < 100 or new_height < 100:
            break
            
        img = img.resize((new_width, new_height), Image.Resampling.LANCZOS)
        
        buffer = io.BytesIO()
        img.save(buffer, format=output_format, quality=50, optimize=True)
        data = buffer.getvalue()
        
        if len(data) <= max_size:
            return data, media_type
    
    buffer = io.BytesIO()
    img.save(buffer, format=output_format, quality=30, optimize=True)
    return buffer.getvalue(), media_type


def load_file(file_path: Path) -> ContentBlock | None:
    if not file_path.exists():
        return None

    ext = file_path.suffix.lower()

    if ext in IMAGE_EXTENSIONS:
        raw_data = file_path.read_bytes()
        
        if len(raw_data) > MAX_IMAGE_SIZE_RAW:
            compressed_data, media_type = compress_image(file_path)
            original_mb = len(raw_data) / (1024 * 1024)
            compressed_mb = len(compressed_data) / (1024 * 1024)
            print(f"\033[33m  Compressed: {original_mb:.1f}MB -> {compressed_mb:.1f}MB\033[0m")
            data = base64.b64encode(compressed_data).decode("ascii")
        else:
            data = base64.b64encode(raw_data).decode("ascii")
            media_type = get_media_type(ext)
        
        return {
            "type": "image",
            "source": {"type": "base64", "media_type": media_type, "data": data},
        }

    content = file_path.read_text()
    return {"type": "text", "text": f'<file name="{file_path.name}">\n{content}\n</file>'}


def open_browser(url: str) -> None:
    system = platform.system().lower()
    if system == "darwin":
        subprocess.Popen(["open", url], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    elif system == "windows":
        subprocess.Popen(["start", url], shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    else:
        subprocess.Popen(["xdg-open", url], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def cmd_login() -> None:
    print("Starting OAuth flow...\n")

    result = start_oauth_flow()

    print("Opening browser...\n")
    open_browser(result.auth_url)

    print("If browser did not open, visit this URL:\n")
    print(result.auth_url)
    print("\nAfter authorizing, you will receive a code in format: CODE#STATE")
    print("Copy the entire code including the # part.\n")

    auth_code = input("Paste the authorization code here: ")

    try:
        credentials = exchange_code_for_tokens(auth_code, result.code_verifier)
        print("\nSuccess! You are now authenticated.")
        print(f"Credentials saved to: {get_config_dir()}")
        print(f"Token expires: {datetime.fromtimestamp(credentials.expires_at / 1000)}")
    except Exception as e:
        print(f"\nAuthentication failed: {e}")
        sys.exit(1)


def cmd_logout() -> None:
    clear_credentials()
    print("Credentials cleared.")


def cmd_status() -> None:
    if not has_valid_credentials():
        print('Not authenticated. Run "login" to authenticate.')
        return

    credentials = load_credentials()
    if credentials:
        print("Authenticated")
        print(f"Token expires: {datetime.fromtimestamp(credentials.expires_at / 1000)}")
        print(f"Connected since: {datetime.fromtimestamp(credentials.connected_at / 1000)}")
        print(f"Config directory: {get_config_dir()}")


async def _stream_and_print(stream: ChatStream) -> None:
    async for chunk in stream:
        print(chunk, end="", flush=True)


def cmd_ask(question: str, file_paths: list[str] | None = None, model: str = "sonnet") -> None:
    if not has_valid_credentials():
        print('Not authenticated. Run "login" first.')
        sys.exit(1)

    if model not in MODELS:
        print(f"\033[31mUnknown model: {model}. Use: {', '.join(MODELS.keys())}\033[0m")
        sys.exit(1)

    content: list[ContentBlock] = []
    
    if file_paths:
        for fp in file_paths:
            resolved = Path(fp).resolve()
            block = load_file(resolved)
            if block:
                print(f"\033[32m> Attached: {resolved.name}\033[0m")
                content.append(block)
            else:
                print(f"\033[31m> File not found: {fp}\033[0m")
                sys.exit(1)
    
    content.append({"type": "text", "text": question})

    print(f"\033[36m[{model}]\033[0m Claude: ", end="", flush=True)
    start_time = time.perf_counter()

    messages: list[Message] = [{"role": "user", "content": content}]
    stream = chat_stream(messages, ChatOptions(model=MODELS[model]))

    asyncio.run(_stream_and_print(stream))

    elapsed = time.perf_counter() - start_time
    if stream.response:
        r = stream.response
        tokens_per_sec = r.output_tokens / elapsed if elapsed > 0 else 0
        print(f"\n\n[{r.input_tokens} in / {r.output_tokens} out | {tokens_per_sec:.1f} tok/s | {elapsed:.1f}s]")


def cmd_chat() -> None:
    if not has_valid_credentials():
        print('Not authenticated. Run "login" first.')
        sys.exit(1)

    messages: list[Message] = []
    current_model = "sonnet"
    pending_files: list[ContentBlock] = []

    print("\033[1mClaude Chat\033[0m")
    print("\033[90mType /help for commands, Ctrl+C to exit\033[0m")
    print("")

    def print_help() -> None:
        print("")
        print("\033[90m|  \033[1mCommands\033[0m")
        print("\033[90m|  /haiku          Switch to Haiku\033[0m")
        print("\033[90m|  /sonnet         Switch to Sonnet\033[0m")
        print("\033[90m|  /opus           Switch to Opus\033[0m")
        print("\033[90m|  /file <path>    Attach file (image/text)\033[0m")
        print("\033[90m|  /clear          Clear attached files\033[0m")
        print("\033[90m|  /help           Show this help\033[0m")
        print("\033[90m|  /exit           Exit\033[0m")
        print("")

    def send_message(user_input: str) -> None:
        nonlocal pending_files, current_model

        lower = user_input.lower().strip()

        if lower == "/haiku":
            current_model = "haiku"
            print(f"\033[32m> Switched to {current_model}\033[0m")
            return
        if lower == "/sonnet":
            current_model = "sonnet"
            print(f"\033[32m> Switched to {current_model}\033[0m")
            return
        if lower == "/opus":
            current_model = "opus"
            print(f"\033[32m> Switched to {current_model}\033[0m")
            return
        if lower == "/help":
            print_help()
            return
        if lower == "/clear":
            pending_files = []
            print("\033[32m> Cleared attached files\033[0m")
            return
        if lower.startswith("/file "):
            file_path = Path(user_input[6:].strip()).resolve()
            block = load_file(file_path)
            if block:
                pending_files.append(block)
                file_type = "img" if block["type"] == "image" else "txt"
                print(f"\033[32m> Attached: [{file_type}] {file_path.name}\033[0m")
            else:
                print(f"\033[31m> File not found: {file_path}\033[0m")
            return
        if lower in ("/exit", "exit"):
            print("Goodbye!")
            sys.exit(0)

        content: list[ContentBlock] = [*pending_files, {"type": "text", "text": user_input}]
        pending_files = []
        messages.append({"role": "user", "content": content})

        try:
            print("\033[36mClaude:\033[0m ", end="", flush=True)

            start_time = time.perf_counter()
            full_response = ""

            stream = chat_stream(messages, ChatOptions(model=MODELS[current_model]))

            async def collect() -> str:
                nonlocal full_response
                async for chunk in stream:
                    print(chunk, end="", flush=True)
                    full_response += chunk
                return full_response

            asyncio.run(collect())

            elapsed = time.perf_counter() - start_time
            if stream.response:
                r = stream.response
                tokens_per_sec = r.output_tokens / elapsed if elapsed > 0 else 0
                print(f"\n\n\033[90m[{r.input_tokens} in / {r.output_tokens} out | {tokens_per_sec:.1f} tok/s | {elapsed:.1f}s]\033[0m")
                print("")
                messages.append({"role": "assistant", "content": full_response})

        except Exception as e:
            print(f"\n\033[31mError: {e}\033[0m")
            print("")

    try:
        while True:
            file_indicator = f"\033[35m[{len(pending_files)} files]\033[0m " if pending_files else ""
            model_indicator = f"\033[90m[{current_model}]\033[0m"
            try:
                user_input = input(f"{file_indicator}{model_indicator} You: ").strip()
            except EOFError:
                print("\nGoodbye!")
                break

            if not user_input:
                continue

            send_message(user_input)

    except KeyboardInterrupt:
        print("\nGoodbye!")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Claude OAuth Client",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  claude-oauth login
  claude-oauth ask "What is 2+2?"
  claude-oauth ask -m haiku "Quick question"
  claude-oauth ask -m opus -f image.png "Analyze this"
  claude-oauth chat
""",
    )

    subparsers = parser.add_subparsers(dest="command", help="Available commands")

    subparsers.add_parser("login", help="Start OAuth flow to authenticate with Claude")
    subparsers.add_parser("logout", help="Clear stored credentials")
    subparsers.add_parser("status", help="Check authentication status")
    subparsers.add_parser("chat", help="Interactive chat with Claude")

    ask_parser = subparsers.add_parser("ask", help="Send a single message")
    ask_parser.add_argument("question", nargs="+", help="Your question")
    ask_parser.add_argument("-f", "--file", action="append", dest="files", help="Attach file (can use multiple times)")
    ask_parser.add_argument("-m", "--model", default="sonnet", choices=["haiku", "sonnet", "opus"], help="Model to use (default: sonnet)")

    args = parser.parse_args()

    if args.command == "login":
        cmd_login()
    elif args.command == "logout":
        cmd_logout()
    elif args.command == "status":
        cmd_status()
    elif args.command == "chat":
        cmd_chat()
    elif args.command == "ask":
        cmd_ask(" ".join(args.question), args.files, args.model)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()

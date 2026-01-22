import { ANTHROPIC_API_URL } from './constants'
import { getValidAccessToken } from './oauth'

export type ContentBlock =
  | { type: 'text'; text: string }
  | { type: 'image'; source: { type: 'base64'; media_type: string; data: string } }

export interface Message {
  role: 'user' | 'assistant'
  content: string | ContentBlock[]
}

export interface ChatOptions {
  model?: string
  maxTokens?: number
  system?: string
  stream?: boolean
}

export interface ChatResponse {
  id: string
  content: string
  model: string
  stopReason: string
  usage: {
    inputTokens: number
    outputTokens: number
  }
}

interface AnthropicMessageResponse {
  id: string
  type: string
  role: string
  content: Array<{ type: string; text: string }>
  model: string
  stop_reason: string
  usage: {
    input_tokens: number
    output_tokens: number
  }
}

// const DEFAULT_MODEL = 'claude-haiku-4-5'
const DEFAULT_MODEL = 'claude-opus-4-5'
const DEFAULT_MAX_TOKENS = 4096
const CLAUDE_CODE_SYSTEM_PREFIX = "You are Claude Code, Anthropic's official CLI for Claude."
const ANTHROPIC_BETA_FLAGS = 'oauth-2025-04-20,interleaved-thinking-2025-05-14'
const USER_AGENT = 'claude-cli/2.1.2 (external, cli)'

export async function chat(
  messages: Message[],
  options: ChatOptions = {}
): Promise<ChatResponse> {
  const accessToken = await getValidAccessToken()

  const systemPrompt = options.system
    ? `${CLAUDE_CODE_SYSTEM_PREFIX}\n\n${options.system}`
    : CLAUDE_CODE_SYSTEM_PREFIX

  const response = await fetch(`${ANTHROPIC_API_URL}/messages?beta=true`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${accessToken}`,
      'anthropic-version': '2023-06-01',
      'anthropic-beta': ANTHROPIC_BETA_FLAGS,
      'user-agent': USER_AGENT,
    },
    body: JSON.stringify({
      model: options.model ?? DEFAULT_MODEL,
      max_tokens: options.maxTokens ?? DEFAULT_MAX_TOKENS,
      system: systemPrompt,
      messages: messages.map((m) => ({
        role: m.role,
        content: m.content,
      })),
    }),
  })

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(`API request failed: ${response.status} - ${errorText}`)
  }

  const data = (await response.json()) as AnthropicMessageResponse

  return {
    id: data.id,
    content: data.content[0]?.text ?? '',
    model: data.model,
    stopReason: data.stop_reason,
    usage: {
      inputTokens: data.usage.input_tokens,
      outputTokens: data.usage.output_tokens,
    },
  }
}

export async function* chatStream(
  messages: Message[],
  options: ChatOptions = {}
): AsyncGenerator<string, ChatResponse> {
  const accessToken = await getValidAccessToken()

  const systemPrompt = options.system
    ? `${CLAUDE_CODE_SYSTEM_PREFIX}\n\n${options.system}`
    : CLAUDE_CODE_SYSTEM_PREFIX

  const response = await fetch(`${ANTHROPIC_API_URL}/messages?beta=true`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${accessToken}`,
      'anthropic-version': '2023-06-01',
      'anthropic-beta': ANTHROPIC_BETA_FLAGS,
      'user-agent': USER_AGENT,
    },
    body: JSON.stringify({
      model: options.model ?? DEFAULT_MODEL,
      max_tokens: options.maxTokens ?? DEFAULT_MAX_TOKENS,
      system: systemPrompt,
      stream: true,
      messages: messages.map((m) => ({
        role: m.role,
        content: m.content,
      })),
    }),
  })

  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(`API request failed: ${response.status} - ${errorText}`)
  }

  const reader = response.body?.getReader()
  if (!reader) throw new Error('No response body')

  const decoder = new TextDecoder()
  let buffer = ''
  let fullContent = ''
  let finalResponse: Partial<ChatResponse> = {}

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n')
    buffer = lines.pop() ?? ''

    for (const line of lines) {
      if (line.startsWith('data: ')) {
        const jsonStr = line.slice(6)
        if (jsonStr === '[DONE]') continue

        try {
          const event = JSON.parse(jsonStr)

          if (event.type === 'content_block_delta' && event.delta?.text) {
            fullContent += event.delta.text
            yield event.delta.text
          }

          if (event.type === 'message_start' && event.message) {
            finalResponse.id = event.message.id
            finalResponse.model = event.message.model
          }

          if (event.type === 'message_delta') {
            finalResponse.stopReason = event.delta?.stop_reason
            if (event.usage) {
              finalResponse.usage = {
                inputTokens: event.usage.input_tokens ?? 0,
                outputTokens: event.usage.output_tokens ?? 0,
              }
            }
          }
        } catch {
          continue
        }
      }
    }
  }

  return {
    id: finalResponse.id ?? '',
    content: fullContent,
    model: finalResponse.model ?? '',
    stopReason: finalResponse.stopReason ?? '',
    usage: finalResponse.usage ?? { inputTokens: 0, outputTokens: 0 },
  }
}

export async function ask(prompt: string, options: ChatOptions = {}): Promise<string> {
  const response = await chat([{ role: 'user', content: prompt }], options)
  return response.content
}

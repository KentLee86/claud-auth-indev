import { test, expect, describe } from 'bun:test'
import path from 'path'
import { loadFile } from './cli-utils'
import { chat } from './lib/client'
import { hasValidCredentials } from './lib/storage'
import type { ContentBlock } from './lib/client'

const TEST_FILES_DIR = path.join(import.meta.dir, '__fixtures__')

describe('vision API integration', () => {
  test('analyzes cat.jpg image correctly', async () => {
    const hasAuth = await hasValidCredentials()
    if (!hasAuth) {
      console.log('Skipping vision test: not authenticated')
      return
    }

    const imageBlock = await loadFile(path.join(TEST_FILES_DIR, 'cat.jpg'))
    expect(imageBlock).not.toBeNull()
    expect(imageBlock?.type).toBe('image')

    const content: ContentBlock[] = [
      imageBlock!,
      { type: 'text', text: 'What animal is in this image? Reply with just the animal name in one word.' }
    ]

    const response = await chat(
      [{ role: 'user', content }],
      { model: 'claude-sonnet-4-20250514', maxTokens: 50 }
    )

    const answer = response.content.toLowerCase()
    expect(answer).toContain('cat')
  }, 30000)
})

import { test, expect, describe } from 'bun:test'
import path from 'path'

const TEST_FILES_DIR = path.join(import.meta.dir, '__fixtures__')

describe('file loading', () => {
  test('loads text file as ContentBlock', async () => {
    const { loadFile } = await import('./cli-utils')
    const result = await loadFile(path.join(TEST_FILES_DIR, 'sample.txt'))
    
    expect(result).not.toBeNull()
    expect(result?.type).toBe('text')
    if (result?.type === 'text') {
      expect(result.text).toContain('Hello from test file')
    }
  })

  test('loads image file as base64 ContentBlock', async () => {
    const { loadFile } = await import('./cli-utils')
    const result = await loadFile(path.join(TEST_FILES_DIR, 'sample.png'))
    
    expect(result).not.toBeNull()
    expect(result?.type).toBe('image')
    if (result?.type === 'image') {
      expect(result.source.type).toBe('base64')
      expect(result.source.media_type).toBe('image/png')
      expect(result.source.data.length).toBeGreaterThan(0)
    }
  })

  test('returns null for non-existent file', async () => {
    const { loadFile } = await import('./cli-utils')
    const result = await loadFile('/nonexistent/path/file.txt')
    
    expect(result).toBeNull()
  })

  test('detects correct media type for jpg', async () => {
    const { getMediaType } = await import('./cli-utils')
    
    expect(getMediaType('.jpg')).toBe('image/jpeg')
    expect(getMediaType('.jpeg')).toBe('image/jpeg')
    expect(getMediaType('.png')).toBe('image/png')
    expect(getMediaType('.gif')).toBe('image/gif')
    expect(getMediaType('.webp')).toBe('image/webp')
  })
})

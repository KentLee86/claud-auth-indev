import path from 'path'
import type { ContentBlock } from './lib/client'

const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.gif', '.webp']

export function getMediaType(ext: string): string {
  const types: Record<string, string> = {
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.jpeg': 'image/jpeg',
    '.gif': 'image/gif',
    '.webp': 'image/webp',
  }
  return types[ext] ?? 'application/octet-stream'
}

export async function loadFile(filePath: string): Promise<ContentBlock | null> {
  const file = Bun.file(filePath)
  if (!(await file.exists())) {
    return null
  }

  const ext = path.extname(filePath).toLowerCase()

  if (IMAGE_EXTENSIONS.includes(ext)) {
    const buffer = await file.arrayBuffer()
    const base64 = Buffer.from(buffer).toString('base64')
    return {
      type: 'image',
      source: {
        type: 'base64',
        media_type: getMediaType(ext),
        data: base64,
      },
    }
  }

  const content = await file.text()
  const fileName = path.basename(filePath)
  return {
    type: 'text',
    text: `<file name="${fileName}">\n${content}\n</file>`,
  }
}

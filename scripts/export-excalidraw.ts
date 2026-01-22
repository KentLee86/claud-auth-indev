#!/usr/bin/env bun
/**
 * Export Excalidraw file to PNG using Playwright
 * Usage: bun scripts/export-excalidraw.ts <input.excalidraw> [output.png]
 */

import { chromium } from 'playwright'

async function exportToPng(inputPath: string, outputPath: string) {
  const file = Bun.file(inputPath)
  if (!(await file.exists())) {
    console.error(`File not found: ${inputPath}`)
    process.exit(1)
  }

  const excalidrawData = await file.json()
  console.log(`Loaded ${excalidrawData.elements?.length || 0} elements from ${inputPath}`)

  const browser = await chromium.launch({ headless: true })
  const page = await browser.newPage()

  // Navigate to Excalidraw
  await page.goto('https://excalidraw.com')
  await page.waitForLoadState('networkidle')

  // Load the data via drag & drop simulation
  await page.evaluate(async (data) => {
    const jsonStr = JSON.stringify(data)
    const blob = new Blob([jsonStr], { type: 'application/json' })
    const file = new File([blob], 'drawing.excalidraw', { type: 'application/json' })
    const dataTransfer = new DataTransfer()
    dataTransfer.items.add(file)

    const canvas = document.querySelector('.excalidraw__canvas')
    if (canvas) {
      const dropEvent = new DragEvent('drop', {
        bubbles: true,
        cancelable: true,
        dataTransfer: dataTransfer,
      })
      canvas.dispatchEvent(dropEvent)
    }
  }, excalidrawData)

  // Wait for elements to render
  await page.waitForTimeout(2000)

  // Click "Scroll back to content" if available
  try {
    const scrollButton = page.getByRole('button', { name: 'Scroll back to content' })
    if (await scrollButton.isVisible({ timeout: 1000 })) {
      await scrollButton.click()
      await page.waitForTimeout(500)
    }
  } catch {
    // Button not available, continue
  }

  // Export using keyboard shortcut (Ctrl+Shift+E for export)
  await page.keyboard.press('Control+Shift+e')
  await page.waitForTimeout(1000)

  // Take screenshot of the dialog or use the export API
  // Alternative: Take a screenshot of the canvas directly
  const canvas = page.locator('.excalidraw__canvas.interactive')
  await canvas.screenshot({ path: outputPath })

  console.log(`Exported to ${outputPath}`)

  await browser.close()
}

// Parse arguments
const args = process.argv.slice(2)
if (args.length < 1) {
  console.log('Usage: bun scripts/export-excalidraw.ts <input.excalidraw> [output.png]')
  process.exit(1)
}

const inputPath = args[0]
const outputPath = args[1] || inputPath.replace(/\.excalidraw$/, '.png')

exportToPng(inputPath, outputPath)

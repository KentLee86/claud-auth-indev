#!/usr/bin/env bun
import {
  startOAuthFlow,
  exchangeCodeForTokens,
  getValidAccessToken,
} from './lib/oauth'
import {
  loadCredentials,
  clearCredentials,
  hasValidCredentials,
  getConfigDir,
} from './lib/storage'
import { chat, chatStream, type Message, type ContentBlock } from './lib/client'
import { loadFile } from './cli-utils'
import path from 'path'

const MODELS = {
  haiku: 'claude-haiku-4-5-20251001',
  sonnet: 'claude-sonnet-4-20250514',
  opus: 'claude-opus-4-20250514',
} as const

type ModelKey = keyof typeof MODELS

const HELP = `
Claude OAuth Client

Commands:
  login     Start OAuth flow to authenticate with Claude
  logout    Clear stored credentials
  status    Check authentication status
  chat      Interactive chat with Claude
  ask       Send a single message (usage: ask "your question")
  help      Show this help message

Examples:
  bun run src/cli.ts login
  bun run src/cli.ts ask "What is 2+2?"
  bun run src/cli.ts chat
`

async function openBrowser(url: string) {
  const { spawn } = await import('child_process')
  const platform = process.platform
  const cmd = platform === 'darwin' ? 'open' : platform === 'win32' ? 'start' : 'xdg-open'
  spawn(cmd, [url], { detached: true, stdio: 'ignore' }).unref()
}

async function login() {
  console.log('Starting OAuth flow...\n')
  
  const { authUrl, codeVerifier } = startOAuthFlow()
  
  console.log('Opening browser...\n')
  await openBrowser(authUrl)
  
  console.log('If browser did not open, visit this URL:\n')
  console.log(authUrl)
  console.log('\nAfter authorizing, you will receive a code in format: CODE#STATE')
  console.log('Copy the entire code including the # part.\n')
  
  const rl = require('readline').createInterface({
    input: process.stdin,
    output: process.stdout,
  })

  const authCode = await new Promise<string>((resolve) => {
    rl.question('Paste the authorization code here: ', (answer: string) => {
      rl.close()
      resolve(answer)
    })
  })

  try {
    const credentials = await exchangeCodeForTokens(authCode, codeVerifier)
    console.log('\nSuccess! You are now authenticated.')
    console.log(`Credentials saved to: ${getConfigDir()}`)
    console.log(`Token expires: ${new Date(credentials.expiresAt).toLocaleString()}`)
  } catch (error) {
    console.error('\nAuthentication failed:', error)
    process.exit(1)
  }
}

async function logout() {
  await clearCredentials()
  console.log('Credentials cleared.')
}

async function status() {
  const hasValid = await hasValidCredentials()
  if (!hasValid) {
    console.log('Not authenticated. Run "login" to authenticate.')
    return
  }

  const credentials = await loadCredentials()
  if (credentials) {
    console.log('Authenticated')
    console.log(`Token expires: ${new Date(credentials.expiresAt).toLocaleString()}`)
    console.log(`Connected since: ${new Date(credentials.connectedAt).toLocaleString()}`)
    console.log(`Config directory: ${getConfigDir()}`)
  }
}

async function interactiveChat() {
  const hasValid = await hasValidCredentials()
  if (!hasValid) {
    console.log('Not authenticated. Run "login" first.')
    process.exit(1)
  }

  const readline = await import('readline')
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
  })

  if (process.stdin.isTTY) {
    process.stdin.setRawMode(true)
  }
  readline.emitKeypressEvents(process.stdin)

  const messages: Message[] = []
  let currentModel: ModelKey = 'sonnet'
  let inputBuffer = ''
  let cursorPos = 0
  let pendingFiles: ContentBlock[] = []

  const clearLine = () => {
    process.stdout.write('\r\x1b[K')
  }

  const renderPrompt = () => {
    clearLine()
    const fileIndicator = pendingFiles.length > 0 ? `\x1b[35m📎${pendingFiles.length}\x1b[0m ` : ''
    const modelIndicator = `\x1b[90m[${currentModel}]\x1b[0m`
    process.stdout.write(`${fileIndicator}${modelIndicator} You: ${inputBuffer}`)
    const promptLen = (pendingFiles.length > 0 ? 4 : 0) + `[${currentModel}] You: `.length
    const moveBack = inputBuffer.length - cursorPos
    if (moveBack > 0) {
      process.stdout.write(`\x1b[${moveBack}D`)
    }
  }

  const printHelp = () => {
    console.log('\n')
    console.log('\x1b[90m┌──────────────────────────────────────────────┐\x1b[0m')
    console.log('\x1b[90m│\x1b[0m  \x1b[1mCommands\x1b[0m                                     \x1b[90m│\x1b[0m')
    console.log('\x1b[90m├──────────────────────────────────────────────┤\x1b[0m')
    console.log('\x1b[90m│\x1b[0m  \x1b[33m/haiku\x1b[0m          Switch to Haiku             \x1b[90m│\x1b[0m')
    console.log('\x1b[90m│\x1b[0m  \x1b[33m/sonnet\x1b[0m         Switch to Sonnet            \x1b[90m│\x1b[0m')
    console.log('\x1b[90m│\x1b[0m  \x1b[33m/opus\x1b[0m           Switch to Opus              \x1b[90m│\x1b[0m')
    console.log('\x1b[90m│\x1b[0m  \x1b[33m/file <path>\x1b[0m    Attach file (image/text)   \x1b[90m│\x1b[0m')
    console.log('\x1b[90m│\x1b[0m  \x1b[33m/clear\x1b[0m          Clear attached files        \x1b[90m│\x1b[0m')
    console.log('\x1b[90m│\x1b[0m  \x1b[33m/help\x1b[0m           Show this help              \x1b[90m│\x1b[0m')
    console.log('\x1b[90m│\x1b[0m  \x1b[33m/exit\x1b[0m           Exit                        \x1b[90m│\x1b[0m')
    console.log('\x1b[90m└──────────────────────────────────────────────┘\x1b[0m')
    console.log('')
    renderPrompt()
  }

  const handleCommand = async (cmd: string): Promise<boolean> => {
    const lower = cmd.toLowerCase()
    if (lower === '/haiku') {
      switchModel('haiku')
      return true
    }
    if (lower === '/sonnet') {
      switchModel('sonnet')
      return true
    }
    if (lower === '/opus') {
      switchModel('opus')
      return true
    }
    if (lower === '/help') {
      printHelp()
      return true
    }
    if (lower === '/clear') {
      pendingFiles = []
      clearLine()
      console.log('\x1b[32m✓ Cleared attached files\x1b[0m')
      renderPrompt()
      return true
    }
    if (lower.startsWith('/file ')) {
      const filePath = cmd.slice(6).trim()
      const resolved = path.resolve(filePath)
      const block = await loadFile(resolved)
      if (block) {
        pendingFiles.push(block)
        const fileName = path.basename(resolved)
        const fileType = block.type === 'image' ? '🖼️' : '📄'
        clearLine()
        console.log(`\x1b[32m✓ Attached: ${fileType} ${fileName}\x1b[0m`)
      } else {
        clearLine()
        console.log(`\x1b[31m✗ File not found: ${filePath}\x1b[0m`)
      }
      renderPrompt()
      return true
    }
    if (lower === '/exit' || lower === 'exit') {
      console.log('\nGoodbye!')
      process.exit(0)
    }
    return false
  }

  const sendMessage = async () => {
    const trimmed = inputBuffer.trim()
    inputBuffer = ''
    cursorPos = 0

    if (!trimmed) {
      renderPrompt()
      return
    }

    if (await handleCommand(trimmed)) {
      return
    }

    const content: ContentBlock[] = [...pendingFiles, { type: 'text', text: trimmed }]
    pendingFiles = []
    messages.push({ role: 'user', content })
    console.log('')

    try {
      process.stdout.write('\x1b[36mClaude:\x1b[0m ')
      
      const stream = chatStream(messages, { model: MODELS[currentModel] })
      let fullResponse = ''
      const startTime = performance.now()
      
      let result = await stream.next()
      while (!result.done) {
        process.stdout.write(result.value)
        fullResponse += result.value
        result = await stream.next()
      }
      
      const elapsed = (performance.now() - startTime) / 1000
      const response = result.value
      const tokensPerSec = response.usage.outputTokens / elapsed
      
      console.log('\n')
      console.log(`\x1b[90m[${response.usage.inputTokens} in / ${response.usage.outputTokens} out | ${tokensPerSec.toFixed(1)} tok/s | ${elapsed.toFixed(1)}s]\x1b[0m`)
      console.log('')
      messages.push({ role: 'assistant', content: fullResponse })
    } catch (error) {
      console.error('\n\x1b[31mError:\x1b[0m', error)
      console.log('')
    }

    renderPrompt()
  }

  const switchModel = (model: ModelKey) => {
    currentModel = model
    clearLine()
    console.log(`\x1b[32m✓ Switched to ${model}\x1b[0m`)
    renderPrompt()
  }

  process.stdin.on('keypress', async (str, key) => {
    if (!key) return

    if (key.ctrl && key.name === 'c') {
      console.log('\nGoodbye!')
      process.exit(0)
    }

    if (key.name === 'return') {
      await sendMessage()
      return
    }

    if (key.name === 'backspace') {
      if (cursorPos > 0) {
        inputBuffer = inputBuffer.slice(0, cursorPos - 1) + inputBuffer.slice(cursorPos)
        cursorPos--
        renderPrompt()
      }
      return
    }

    if (key.name === 'delete') {
      if (cursorPos < inputBuffer.length) {
        inputBuffer = inputBuffer.slice(0, cursorPos) + inputBuffer.slice(cursorPos + 1)
        renderPrompt()
      }
      return
    }

    if (key.name === 'left') {
      if (cursorPos > 0) {
        cursorPos--
        renderPrompt()
      }
      return
    }
    if (key.name === 'right') {
      if (cursorPos < inputBuffer.length) {
        cursorPos++
        renderPrompt()
      }
      return
    }

    if (key.name === 'home') {
      cursorPos = 0
      renderPrompt()
      return
    }
    if (key.name === 'end') {
      cursorPos = inputBuffer.length
      renderPrompt()
      return
    }

    if (str && !key.ctrl && !key.meta) {
      inputBuffer = inputBuffer.slice(0, cursorPos) + str + inputBuffer.slice(cursorPos)
      cursorPos += str.length
      renderPrompt()
    }
  })

  console.log('\x1b[1mClaude Chat\x1b[0m')
  console.log('\x1b[90mPress Ctrl+H for help, Ctrl+C to exit\x1b[0m')
  console.log('')
  renderPrompt()
}

async function singleAsk(question: string) {
  const hasValid = await hasValidCredentials()
  if (!hasValid) {
    console.log('Not authenticated. Run "login" first.')
    process.exit(1)
  }

  try {
    process.stdout.write('Claude: ')
    
    const stream = chatStream([{ role: 'user', content: question }])
    const startTime = performance.now()
    
    let result = await stream.next()
    while (!result.done) {
      process.stdout.write(result.value)
      result = await stream.next()
    }
    
    const elapsed = (performance.now() - startTime) / 1000
    const response = result.value
    const tokensPerSec = response.usage.outputTokens / elapsed
    
    console.log('\n')
    console.log(`[${response.usage.inputTokens} in / ${response.usage.outputTokens} out | ${tokensPerSec.toFixed(1)} tok/s | ${elapsed.toFixed(1)}s]`)
  } catch (error) {
    console.error('Error:', error)
    process.exit(1)
  }
}

async function main() {
  const [, , command, ...args] = process.argv

  switch (command) {
    case 'login':
      await login()
      break
    case 'logout':
      await logout()
      break
    case 'status':
      await status()
      break
    case 'chat':
      await interactiveChat()
      break
    case 'ask':
      const question = args.join(' ')
      if (!question) {
        console.log('Usage: ask "your question"')
        process.exit(1)
      }
      await singleAsk(question)
      break
    case 'help':
    case '--help':
    case '-h':
    case undefined:
      console.log(HELP)
      break
    default:
      console.log(`Unknown command: ${command}`)
      console.log(HELP)
      process.exit(1)
  }
}

main().catch(console.error)

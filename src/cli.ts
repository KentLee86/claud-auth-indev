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
import { chat, chatStream, type Message } from './lib/client'

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

  console.log('Interactive chat with Claude. Type "exit" to quit.\n')

  const rl = require('readline').createInterface({
    input: process.stdin,
    output: process.stdout,
  })

  const messages: Message[] = []

  const askQuestion = () => {
    rl.question('You: ', async (input: string) => {
      const trimmed = input.trim()
      
      if (trimmed.toLowerCase() === 'exit') {
        console.log('Goodbye!')
        rl.close()
        return
      }

      if (!trimmed) {
        askQuestion()
        return
      }

      messages.push({ role: 'user', content: trimmed })

      try {
        process.stdout.write('Claude: ')
        
        const stream = chatStream(messages)
        let fullResponse = ''
        
        for await (const chunk of stream) {
          process.stdout.write(chunk)
          fullResponse += chunk
        }
        
        console.log('\n')
        messages.push({ role: 'assistant', content: fullResponse })
      } catch (error) {
        console.error('\nError:', error)
      }

      askQuestion()
    })
  }

  askQuestion()
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
    
    for await (const chunk of stream) {
      process.stdout.write(chunk)
    }
    
    console.log('')
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

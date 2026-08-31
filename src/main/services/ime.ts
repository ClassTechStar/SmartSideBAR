// services/ime.ts - 输入法服务 (常驻 PowerShell 进程)
//
// C5 修复: 原实现每次 getState/toggle 各 spawn 一个 PowerShell (5s 超时),
// 课堂高频切换下每次数百 ms 延迟 + 频繁进程创建。
// 改为常驻 PowerShell 守护进程: 通过 stdin 发命令 (GET/TOGGLE), stdout 逐行回 JSON,
// Node 侧用 FIFO 队列匹配请求-响应, 进程退出时自动降级并懒重启。

import { spawn, type ChildProcess } from 'child_process'
import log from 'electron-log'
import type { ImeState } from '../../shared/types'

let currentState: ImeState = { locale: 'zh-CN', isChinese: true, mode: 'cn' }
let serviceState: 'idle' | 'starting' | 'running' | 'degraded' = 'idle'

// 常驻守护脚本: 从 stdin 逐行读命令 (GET/TOGGLE), 每行输出一个 JSON 结果。
// $ProgressPreference 消除进度条输出; 显式 Out.Flush 保证逐行到达 Node 侧。
const PS_IME_DAEMON = `
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Windows.Forms
function Get-ImeState {
  $current = [System.Windows.Forms.InputLanguage]::CurrentInputLanguage
  $layout = $current.Culture.Name
  $isChinese = ($layout -match 'zh')
  @{ locale = $layout; isChinese = $isChinese; mode = $(if($isChinese){'cn'}else{'en'}) } | ConvertTo-Json -Compress
}
function Toggle-Ime {
  $current = [System.Windows.Forms.InputLanguage]::CurrentInputLanguage
  $langs = [System.Windows.Forms.InputLanguage]::InstalledInputLanguages
  $currentIsCn = ($current.Culture.Name -match 'zh')
  $target = $null
  if ($currentIsCn) {
    $target = $langs | Where-Object { $_.Culture.Name -match 'en' } | Select-Object -First 1
    if (-not $target) { $target = $langs | Where-Object { $_.Culture.Name -ne $current.Culture.Name } | Select-Object -First 1 }
  } else {
    $target = $langs | Where-Object { $_.Culture.Name -match 'zh' } | Select-Object -First 1
    if (-not $target) { $target = $langs | Where-Object { $_.Culture.Name -ne $current.Culture.Name } | Select-Object -First 1 }
  }
  if ($target) { [System.Windows.Forms.InputLanguage]::CurrentInputLanguage = $target }
  Get-ImeState
}
while ($true) {
  $line = [Console]::In.ReadLine()
  if ($null -eq $line) { exit 0 }
  $cmd = $line.Trim()
  try {
    if ($cmd -eq 'GET') { Get-ImeState }
    elseif ($cmd -eq 'TOGGLE') { Toggle-Ime }
    else { Write-Output '' }
  } catch { Write-Output '' }
  [Console]::Out.Flush()
}
`

function toBase64Command(script: string): string {
  return Buffer.from(script, 'utf16le').toString('base64')
}

// ---- 常驻进程状态 ----
let ps: ChildProcess | null = null
let stdoutBuffer = ''
interface Pending { resolve: (s: ImeState) => void; reject: (e: Error) => void }
const queue: Pending[] = []
const CMD_TIMEOUT_MS = 5000

/** 拒绝所有排队请求 (进程退出/出错时) */
function rejectAllPending(err: Error): void {
  while (queue.length) queue.shift()!.reject(err)
}

/** 惰性启动常驻进程 */
function ensureDaemon(): void {
  if (ps) return
  try {
    const child = spawn('powershell', ['-NoProfile', '-NonInteractive', '-EncodedCommand', toBase64Command(PS_IME_DAEMON)], {
      windowsHide: true,
      stdio: ['pipe', 'pipe', 'pipe']
    })
    ps = child

    child.stdout?.on('data', (data: Buffer) => {
      stdoutBuffer += data.toString()
      let idx: number
      while ((idx = stdoutBuffer.indexOf('\n')) >= 0) {
        const line = stdoutBuffer.slice(0, idx).trim()
        stdoutBuffer = stdoutBuffer.slice(idx + 1)
        const p = queue.shift()
        if (!p) continue
        if (line) {
          try { p.resolve(JSON.parse(line)) }
          catch { p.reject(new Error('IME daemon parse error')) }
        } else {
          p.reject(new Error('IME daemon empty result'))
        }
      }
    })

    child.stderr?.on('data', (d: Buffer) => log.warn('[IME] daemon stderr:', d.toString().trim()))

    child.on('error', (e) => {
      log.warn('[IME] daemon spawn error:', e.message)
      ps = null
      rejectAllPending(new Error(e.message))
    })

    child.on('exit', (code) => {
      log.warn(`[IME] daemon exited (code=${code})`)
      ps = null
      rejectAllPending(new Error(`IME daemon exited (code=${code})`))
    })

    log.info('[IME] Daemon started')
  } catch (e: any) {
    log.warn('[IME] daemon spawn failed:', e.message)
    ps = null
    serviceState = 'degraded'
  }
}

/** 发送命令并等待对应的一行 JSON 结果 */
function sendCommand(cmd: 'GET' | 'TOGGLE'): Promise<ImeState> {
  ensureDaemon()
  if (!ps) return Promise.reject(new Error('IME daemon not available'))

  return new Promise<ImeState>((resolve, reject) => {
    const timer = setTimeout(() => {
      // 超时: 该请求永久移除, 进程视为卡死, 强杀并降级
      const i = queue.indexOf(entry)
      if (i >= 0) queue.splice(i, 1)
      reject(new Error('IME daemon timeout'))
      try { ps?.kill() } catch { /* ignore */ }
      ps = null
    }, CMD_TIMEOUT_MS)

    const entry: Pending = {
      resolve: (s) => { clearTimeout(timer); resolve(s) },
      reject: (e) => { clearTimeout(timer); reject(e) }
    }
    queue.push(entry)
    try {
      ps!.stdin!.write(cmd + '\n')
    } catch (e: any) {
      clearTimeout(timer)
      const i = queue.indexOf(entry)
      if (i >= 0) queue.splice(i, 1)
      reject(e)
    }
  })
}

export const ImeService = {
  async start(): Promise<void> {
    serviceState = 'starting'
    try {
      ensureDaemon()
      const state = await sendCommand('GET')
      currentState = state
      serviceState = 'running'
      log.info('[IME] Service started, current:', state.mode)
    } catch (e) {
      serviceState = 'degraded'
      log.warn('[IME] Service degraded:', e)
    }
  },

  async stop(): Promise<void> {
    serviceState = 'idle'
    if (ps) {
      try { ps.kill() } catch { /* ignore */ }
      ps = null
    }
    rejectAllPending(new Error('IME stopped'))
  },

  async getState(): Promise<ImeState> {
    if (serviceState === 'degraded') return currentState
    try {
      const result = await sendCommand('GET')
      currentState = result
      return result
    } catch (e: any) {
      log.warn('[IME] getState failed:', e.message)
      serviceState = 'degraded'
      return currentState
    }
  },

  async toggle(): Promise<ImeState> {
    try {
      const result = await sendCommand('TOGGLE')
      currentState = result
      log.info('[IME] Toggled to:', result.mode)
      return result
    } catch (e: any) {
      log.warn('[IME] Toggle failed:', e.message)
      serviceState = 'degraded'
      // 降级: 只翻转本地标记状态
      currentState = {
        ...currentState,
        isChinese: !currentState.isChinese,
        mode: currentState.mode === 'cn' ? 'en' : 'cn'
      }
      return currentState
    }
  },

  getServiceState(): string {
    return serviceState
  }
}

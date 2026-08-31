// services/ime.ts - 输入法服务 (PowerShell 兜底)

import { exec } from 'child_process'
import { promisify } from 'util'
import log from 'electron-log'
import type { ImeState } from '../../shared/types'

const execAsync = promisify(exec)

let currentState: ImeState = { locale: 'zh-CN', isChinese: true, mode: 'cn' }
let serviceState: 'idle' | 'starting' | 'running' | 'degraded' = 'idle'

// 获取当前输入法状态
// $ProgressPreference 用于消除进度条输出对 JSON 解析的干扰
// 注: 实际调用的是 Windows PowerShell 5.1 (exec 'powershell'), 脚本语法对 5.1/7.x 均兼容
const PS_GET_IME = `
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Windows.Forms
$current = [System.Windows.Forms.InputLanguage]::CurrentInputLanguage
$layout = $current.Culture.Name
$isChinese = ($layout -eq 'zh-CN' -or $layout -eq 'zh-TW' -or $layout -eq 'zh-Hans' -or $layout -eq 'zh-Hant')
$result = @{ locale = $layout; isChinese = $isChinese; mode = $(if($isChinese){'cn'}else{'en'}) }
$result | ConvertTo-Json -Compress
`

// 获取所有输入法并切换
// $ProgressPreference 用于消除进度条输出对 JSON 解析的干扰
const PS_TOGGLE_IME = `
$ProgressPreference = 'SilentlyContinue'
Add-Type -AssemblyName System.Windows.Forms
$current = [System.Windows.Forms.InputLanguage]::CurrentInputLanguage
$langs = [System.Windows.Forms.InputLanguage]::InstalledInputLanguages
$names = $langs | ForEach-Object { $_.Culture.Name }

# 找下一个输入法: 如果当前是中文类,切到英文; 否则切到第一个中文类
$currentIsCn = ($current.Culture.Name -match 'zh')
$target = $null
if ($currentIsCn) {
  $target = $langs | Where-Object { $_.Culture.Name -match 'en' -or $_.Culture.Name -eq 'en-US' } | Select-Object -First 1
  if (-not $target) { $target = $langs | Where-Object { $_.Culture.Name -ne $current.Culture.Name } | Select-Object -First 1 }
} else {
  $target = $langs | Where-Object { $_.Culture.Name -match 'zh' } | Select-Object -First 1
  if (-not $target) { $target = $langs | Where-Object { $_.Culture.Name -ne $current.Culture.Name } | Select-Object -First 1 }
}

if ($target) {
  [System.Windows.Forms.InputLanguage]::CurrentInputLanguage = $target
}

$newCurrent = [System.Windows.Forms.InputLanguage]::CurrentInputLanguage
$isChinese = ($newCurrent.Culture.Name -match 'zh')
@{ locale = $newCurrent.Culture.Name; isChinese = $isChinese; mode = $(if($isChinese){'cn'}else{'en'}) } | ConvertTo-Json -Compress
`

export const ImeService = {
  async start(): Promise<void> {
    serviceState = 'starting'
    try {
      const state = await this.getState()
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
  },

  async getState(): Promise<ImeState> {
    if (serviceState === 'degraded') return currentState
    try {
      const { stdout } = await execAsync(`powershell -NoProfile -Command "${PS_GET_IME}"`, { timeout: 5000 })
      const result = JSON.parse(stdout.trim())
      currentState = result
      return result
    } catch (e: any) {
      log.warn('[IME] PowerShell getState failed:', e.message)
      return currentState
    }
  },

  async toggle(): Promise<ImeState> {
    try {
      const { stdout } = await execAsync(`powershell -NoProfile -Command "${PS_TOGGLE_IME}"`, { timeout: 5000 })
      const result = JSON.parse(stdout.trim())
      currentState = result
      log.info('[IME] Toggled to:', result.mode)
      return result
    } catch (e: any) {
      log.warn('[IME] Toggle failed:', e.message)
      // 降级: 只切换标记状态
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

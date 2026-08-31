// services/config.ts - 配置服务 (三层覆盖: 默认 -> ProgramData -> AppData)

import { app } from 'electron'
import { join, dirname } from 'path'
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'fs'
import log from 'electron-log'
import type { SidekickConfig, ReminderSoundConfig } from '../../shared/types'

const DEFAULT_CONFIG: SidekickConfig = {
  version: 2,
  power: { launcher: 'side', sleep: '10min', powerOnAt: '07:50' },
  ime: { slot1: 'Microsoft Pinyin', slot2: 'US', fallbackSwap: true },
  capture: { hotkey: 'Ctrl+Shift+A', format: 'PNG', dir: '{Pictures}/Sidekick' },
  recorder: { fps: 15, bitrate: '2M', mic: false, dir: '{Videos}/Sidekick' },
  usb: { enabled: true, ignoreTypes: ['phone', 'carplay'] },
  printer: { pollIntervalSec: 10 },
  display: { sidebarMonitor: 'primary', sidebarSide: 'right', fitWindowsToWorkArea: true },
  links: [
    { id: 'l1', name: '国家中小学智慧教育平台', url: 'https://www.zxx.edu.cn', enabled: true },
    { id: 'l2', name: '希沃白板', url: 'https://easinote.seewo.com', enabled: true },
    { id: 'l3', name: '学科网', url: 'https://www.zxxk.com', enabled: true },
    { id: 'l4', name: '百度文库', url: 'https://wenku.baidu.com', enabled: false },
    { id: 'l5', name: '中国知网', url: 'https://www.cnki.net', enabled: false }
  ],
  reminders: [],
  reminderSound: { preset: 'default', mp3Path: null, volume: 0.8, repeat: 3, repeatInterval: 800 } satisfies ReminderSoundConfig,
  oobe: {
    completed: false,
    completedAt: null,
    skipped: false,
    role: null,
    lastStepIndex: 0,
    prefs: { ime: true, usb: true, shot: true, recorder: false, printer: true },
    env: { screen: { w: 1920, h: 1080, scale: 1, touch: true }, printerCount: 0, imeOk: true, os: 'win32' }
  },
  policy: { disabledModules: [] }
}

function getUserConfigPath(): string {
  return join(app.getPath('userData'), 'config.json')
}

function getProgramDataPath(): string {
  if (process.platform !== 'win32') return ''
  return join(process.env.ProgramData || 'C:\\ProgramData', 'SeewoSidekick', 'config.json')
}

function expandPath(p: string): string {
  return p
    .replace('{Pictures}', app.getPath('pictures'))
    .replace('{Videos}', app.getPath('videos'))
    .replace('{Home}', app.getPath('home'))
}

function deepMerge<T>(base: T, overlay: Partial<T>): T {
  const result = { ...base }
  for (const key in overlay) {
    if (overlay[key] !== undefined && overlay[key] !== null) {
      if (typeof overlay[key] === 'object' && !Array.isArray(overlay[key])) {
        result[key] = deepMerge(result[key] as any, overlay[key] as any)
      } else {
        result[key] = overlay[key] as any
      }
    }
  }
  return result
}

let currentConfig: SidekickConfig | null = null

export const ConfigService = {
  async init(): Promise<SidekickConfig> {
    const userPath = getUserConfigPath()
    const programDataPath = getProgramDataPath()

    // 确保目录存在
    mkdirSync(dirname(userPath), { recursive: true })

    let config = { ...DEFAULT_CONFIG }

    // 第二层: ProgramData 策略覆盖
    if (existsSync(programDataPath)) {
      try {
        const policy = JSON.parse(readFileSync(programDataPath, 'utf-8'))
        config = deepMerge(config, policy)
        log.info('[Config] Loaded ProgramData policy')
      } catch (e) {
        log.warn('[Config] ProgramData parse error:', e)
      }
    }

    // 第三层: 用户配置覆盖
    if (existsSync(userPath)) {
      try {
        const user = JSON.parse(readFileSync(userPath, 'utf-8'))
        config = deepMerge(config, user)
        log.info('[Config] Loaded user config')
      } catch (e) {
        log.warn('[Config] User config parse error, using defaults:', e)
      }
    } else {
      // 首次运行,创建默认配置
      writeFileSync(userPath, JSON.stringify(config, null, 2), 'utf-8')
      log.info('[Config] Created default user config')
    }

    // 展开路径变量
    config.capture.dir = expandPath(config.capture.dir)
    config.recorder.dir = expandPath(config.recorder.dir)

    currentConfig = config
    return config
  },

  get(): SidekickConfig {
    if (!currentConfig) throw new Error('Config not initialized')
    return currentConfig
  },

  set(key: string, value: unknown): boolean {
    if (!currentConfig) return false
    const keys = key.split('.')
    let target: any = currentConfig
    for (let i = 0; i < keys.length - 1; i++) {
      target = target[keys[i]]
    }
    target[keys[keys.length - 1]] = value

    // 持久化到用户目录
    try {
      const userPath = getUserConfigPath()
      writeFileSync(userPath, JSON.stringify(currentConfig, null, 2), 'utf-8')
      return true
    } catch (e) {
      log.error('[Config] Save failed:', e)
      return false
    }
  },

  isModuleDisabled(module: string): boolean {
    if (!currentConfig) return false
    return currentConfig.policy.disabledModules.includes(module)
  }
}

// shared/types.ts - 核心数据模型与类型定义

import type { AppearanceConfig, AppearanceSnapshot } from './appearance'
import type { FloatBallConfig } from './floatball-layout'

export type { AppearanceConfig, AppearanceSnapshot, FloatBallConfig }

export interface Rect {
  x: number
  y: number
  width: number
  height: number
}

export interface RectPx {
  x: number
  y: number
  width: number
  height: number
}

export interface DisplayInfo {
  id: number
  name: string
  bounds: Rect
  workArea: Rect
  sizePx: { w: number; h: number }
  scaleFactor: number
  primary: boolean
}

export interface ImeState {
  locale: string
  isChinese: boolean
  mode: 'cn' | 'en'
  app?: string
}

export interface DriveEvent {
  drive: string
  label?: string
  type?: string
  size?: string
  at: number
}

export interface RecorderStatus {
  recording: boolean
  elapsed: number
  filepath?: string
  error?: string
}

export interface OverlayInit {
  mode: 'region' | 'annotate'
  screenshotPath?: string
  scaleFactor: number
  screenWidth: number
  screenHeight: number
}

export interface PrinterStatus {
  name: string
  state: 'ok' | 'out_of_paper' | 'jammed' | 'offline' | 'low_ink' | 'unknown'
  updatedAt: number
}

export interface LinkItem {
  id: string
  name: string
  url: string
  icon?: string
  enabled: boolean
}

export interface Reminder {
  id: string
  kind: 'once' | 'interval' | 'hourly'
  at: number
  note?: string
  repeatMin?: number
  snoozedUntil?: number
}

export interface ReminderSoundConfig {
  /** 内置音效: 'default' | 'bell' | 'chime' | 'alarm' */
  preset: string
  /** 自定义 MP3 文件路径 */
  mp3Path: string | null
  /** 音量 0~1 */
  volume: number
  /** 重复次数 (1~5) */
  repeat: number
  /** 重复间隔 ms */
  repeatInterval: number
}

export interface EnvResult {
  screen: { w: number; h: number; scale: number; touch: boolean }
  printerCount: number
  imeOk: boolean
  os: string
}

export interface OobeState {
  completed: boolean
  completedAt: string | null
  skipped: boolean
  role: 'teacher' | 'admin' | null
  lastStepIndex: number
  prefs: { ime: boolean; usb: boolean; shot: boolean; recorder: boolean; printer: boolean }
  env: EnvResult
}

export interface SidekickConfig {
  version: number
  ime: {
    slot1: string
    slot2: string
    fallbackSwap: boolean
  }
  capture: {
    hotkey: string
    annotateHotkey: string
    longshotHotkey: string
    format: string
    dir: string
  }
  recorder: {
    fps: number
    bitrate: string
    mic: boolean
    dir: string
  }
  usb: {
    enabled: boolean
    ignoreTypes: string[]
  }
  printer: {
    pollIntervalSec: number
  }
  display: {
    sidebarMonitor: string
    sidebarSide: 'left' | 'right'
    fitWindowsToWorkArea: boolean
  }
  appearance: AppearanceConfig
  floatBall: FloatBallConfig
  links: LinkItem[]
  reminders: Reminder[]
  reminderSound: ReminderSoundConfig
  oobe: OobeState
  policy: {
    disabledModules: string[]
  }
}

export type ServiceState = 'idle' | 'starting' | 'running' | 'degraded' | 'stopped'

export interface NotificationItem {
  id: string
  type: 'info' | 'warn' | 'error' | 'success'
  title: string
  message: string
  duration: number
  timestamp: number
}

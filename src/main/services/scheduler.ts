// services/scheduler.ts - 提醒调度服务 (v2: 更频繁检查 + 自定义铃声)

import log from 'electron-log'
import { BrowserWindow, Notification } from 'electron'
import type { Reminder, ReminderSoundConfig } from '../../shared/types'
import { IPC_CHANNELS } from '../../shared/ipc-channels'
import { ConfigService } from './config'

let timer: NodeJS.Timeout | null = null
let reminders: Reminder[] = []
const firedOnceIds = new Set<string>()

/** 将 PowerShell 脚本编码为 Base64 (UTF-16LE) 以避免引号转义问题 */
function toBase64Command(script: string): string {
  const buf = Buffer.from(script, 'utf16le')
  return buf.toString('base64')
}

export const SchedulerService = {
  async start(): Promise<void> {
    reminders = ConfigService.get().reminders || []
    // 启动时清理已过期的 once 提醒（超过 1 小时未处理）
    const now = Date.now()
    const expired = reminders.filter(
      r => r.kind === 'once' && !r.snoozedUntil && now - r.at > 3600000
    )
    if (expired.length > 0) {
      log.info(`[Scheduler] Cleaning ${expired.length} expired once-reminder(s)`)
      reminders = reminders.filter(r => !expired.includes(r))
      ConfigService.set('reminders', reminders)
    }
    log.info(`[Scheduler] Started with ${reminders.length} reminder(s)`)

    // 每 3 秒检查 — 提升触发精度和响应速度
    timer = setInterval(() => this.tick(), 3000)
    // 启动后立即检查一次
    setTimeout(() => this.tick(), 1000)
  },

  stop(): void {
    if (timer) {
      clearInterval(timer)
      timer = null
    }
    log.info('[Scheduler] Stopped')
  },

  tick(): void {
    const now = Date.now()
    const toRemove: string[] = []
    let changed = false

    for (const r of reminders) {
      // 贪睡中 — 跳过
      if (r.snoozedUntil && now < r.snoozedUntil) continue

      // 贪睡已过期 — 清除标记
      if (r.snoozedUntil && now >= r.snoozedUntil) {
        r.snoozedUntil = undefined
        changed = true
      }

      if (r.kind === 'once' && now >= r.at) {
        if (!firedOnceIds.has(r.id)) {
          this.fire(r)
          firedOnceIds.add(r.id)
          toRemove.push(r.id)
          setTimeout(() => firedOnceIds.delete(r.id), 5000)
        }
      } else if (r.kind === 'interval' && r.repeatMin && now >= r.at) {
        this.fire(r)
        r.at = now + r.repeatMin * 60000
        changed = true
      } else if (r.kind === 'hourly' && now >= r.at) {
        this.fire(r)
        const nextHour = new Date(now)
        nextHour.setHours(nextHour.getHours() + 1, 0, 0, 0)
        r.at = nextHour.getTime()
        changed = true
      }
    }

    if (toRemove.length > 0) {
      reminders = reminders.filter(r => !toRemove.includes(r.id))
      changed = true
      log.info(`[Scheduler] Auto-removed ${toRemove.length} fired once-reminder(s)`)
    }

    // P1-2 (C1 修复): 仅在提醒集合实际变化时落盘
    // 原实现每 3s 无条件 ConfigService.set → 每天约 2.9 万次全量 JSON 写盘
    if (changed) {
      ConfigService.set('reminders', reminders)
    }
  },

  fire(r: Reminder): void {
    log.info(`[Scheduler] Firing reminder: ${r.id} - ${r.note || ''}`)
    // 播放铃声
    const soundConfig = ConfigService.get().reminderSound
    if (soundConfig) {
      this.playSound(soundConfig)
    }
    // 发送通知到所有窗口
    for (const win of BrowserWindow.getAllWindows()) {
      win.webContents.send(IPC_CHANNELS['reminder:due'], r)
    }
    // 系统通知 (silent=true 避免与铃声重复)
    if (Notification.isSupported()) {
      new Notification({
        title: '提醒',
        body: r.note || '您有一条提醒',
        silent: true
      }).show()
    }
  },

  /** 播放提醒铃声 */
  playSound(cfg: ReminderSoundConfig): void {
    const { preset, mp3Path, volume, repeat, repeatInterval } = cfg

    // 如果有自定义 MP3 文件, 使用 ffplay 播放
    if (preset === 'custom' && mp3Path) {
      this.playMp3(mp3Path, volume, repeat, repeatInterval)
      return
    }

    // 内置音效: 使用 PowerShell SystemSounds
    const soundName = preset === 'bell' ? 'Asterisk' : preset === 'alarm' ? 'Exclamation' : 'Asterisk'
    const PS_BEEP = `Add-Type -AssemblyName System.Windows.Forms
$snd = [System.Media.SystemSounds]::${soundName}
for ($i = 0; $i -lt ${repeat}; $i++) {
    $snd.Play()
    Start-Sleep -Milliseconds ${repeatInterval}
}
`
    const { exec } = require('child_process')
    exec(
      `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_BEEP)}"`,
      { timeout: 15000, windowsHide: true },
      (err: any) => { if (err) log.warn('[Scheduler] Sound playback failed:', err.message) }
    )
  },

  /** 使用 ffplay 或系统播放器播放 MP3 */
  playMp3(path: string, volume: number, repeat: number, interval: number): void {
    const { spawn, execSync, exec } = require('child_process')

    // 检查 ffplay 是否可用
    let hasFfplay = false
    try {
      execSync('ffplay -version', { windowsHide: true, stdio: 'ignore' })
      hasFfplay = true
    } catch { hasFfplay = false }

    if (hasFfplay) {
      // 使用 ffplay 精确控制音量 (0-256)
      const vol = Math.max(0, Math.min(1, volume))
      for (let i = 0; i < repeat; i++) {
        setTimeout(() => {
          spawn('ffplay', [
            '-nodisp', '-autoexit', '-volume', String(Math.round(vol * 256)),
            path
          ], { windowsHide: true, stdio: 'ignore' })
        }, i * interval)
      }
    } else {
      // 降级: 使用 Windows Media Player COM
      const safePath = path.replace(/"/g, '`"')
      const vol100 = Math.round(Math.max(0, Math.min(1, volume)) * 100)
      const PS_MP3 = `$player = New-Object -ComObject WMPlayer.OCX
$player.URL = "${safePath}"
$player.settings.volume = ${vol100}
$player.controls.play()
Start-Sleep -Seconds 5
$player.close()
`
      exec(
        `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_MP3)}"`,
        { timeout: 10000, windowsHide: true },
        (err: any) => { if (err) log.warn('[Scheduler] MP3 playback failed:', err.message) }
      )
    }
  },

  add(r: Reminder): void {
    const idx = reminders.findIndex(existing => existing.id === r.id)
    if (idx >= 0) {
      reminders[idx] = r
      log.info(`[Scheduler] Updated reminder: ${r.id}`)
    } else {
      reminders.push(r)
      log.info(`[Scheduler] Added reminder: ${r.id}`)
    }
    firedOnceIds.delete(r.id)
    ConfigService.set('reminders', reminders)
  },

  remove(id: string): void {
    reminders = reminders.filter(r => r.id !== id)
    firedOnceIds.delete(id)
    ConfigService.set('reminders', reminders)
    log.info(`[Scheduler] Removed reminder: ${id}`)
  },

  list(): Reminder[] {
    return reminders
  }
}

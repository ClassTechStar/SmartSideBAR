// services/recorder.ts - 录屏服务 (修复竞态条件和启动延迟)
// 核心改动: ready监听器在loadFile之前注册,避免信号丢失;新增starting状态

import { writeFileSync, existsSync, mkdirSync } from 'fs'
import { join } from 'path'
import { app, BrowserWindow, desktopCapturer, ipcMain, screen, Notification } from 'electron'
import log from 'electron-log'
import { ConfigService } from './config'
import { WindowManager } from '../windows/manager'
import { IPC_CHANNELS } from '../../shared/ipc-channels'

let recorderWin: BrowserWindow | null = null
let isRecording = false
let isStarting = false // 新增: 启动中状态,防止UI延迟
let recordStartTime = 0
let elapsedTimer: NodeJS.Timeout | null = null

function pad(n: number): string {
  return String(n).padStart(2, '0')
}

export const RecorderService = {
  isRecording(): boolean {
    return isRecording
  },

  isStarting(): boolean { // 新增: UI 查询启动状态
    return isStarting
  },

  getStatus(): { recording: boolean; starting: boolean; elapsed: number } {
    return {
      recording: isRecording,
      starting: isStarting,
      elapsed: isRecording ? Math.round((Date.now() - recordStartTime) / 1000) : 0
    }
  },

  async start(options?: { fps?: number; mic?: boolean }): Promise<{ success: boolean; error?: string }> {
    if (isRecording || isStarting) {
      return { success: false, error: '录屏已在进行中' }
    }

    isStarting = true
    // 立即广播状态变更,UI可显示"启动中..."
    WindowManager.broadcast(IPC_CHANNELS['recorder:statusChanged'], this.getStatus())

    try {
      const cfg = ConfigService.get()
      const fps = options?.fps || cfg.recorder.fps || 15
      const mic = options?.mic ?? cfg.recorder.mic ?? false

      // 创建隐藏的录屏窗口
      recorderWin = new BrowserWindow({
        x: -10000,
        y: -10000,
        width: 1,
        height: 1,
        show: false,
        frame: false,
        skipTaskbar: true,
        webPreferences: {
          preload: join(__dirname, '../preload/index.js'),
          nodeIntegration: false,
          contextIsolation: true,
          sandbox: false
        }
      })

      // 设置 displayMedia 请求处理器 (绕过用户选择对话框)
      recorderWin.webContents.session.setDisplayMediaRequestHandler((_request, callback) => {
        desktopCapturer.getSources({ types: ['screen'] }).then((sources) => {
          if (sources.length > 0) {
            callback({ video: sources[0], audio: mic ? 'loopback' : undefined })
          } else {
            callback({})
          }
        }).catch((e) => {
          log.error('[Recorder] desktopCapturer error:', e)
          callback({})
        })
      })

      // ========== 关键修复: 先注册监听器,再加载页面 ==========
      // 这样页面加载完成发出的 ready 信号不会丢失
      let pageReadyResolve: (() => void) | null = null
      let pageReadyReject: ((err: Error) => void) | null = null

      const pageReadyPromise = new Promise<void>((resolve, reject) => {
        pageReadyResolve = resolve
        pageReadyReject = reject
      })

      const readyHandler = () => {
        log.info('[Recorder] Page ready signal received')
        if (pageReadyResolve) pageReadyResolve()
      }
      ipcMain.on(IPC_CHANNELS['overlay:ready'], readyHandler)

      // 设置10秒超时
      const loadTimeout = setTimeout(() => {
        ipcMain.removeListener(IPC_CHANNELS['overlay:ready'], readyHandler)
        if (pageReadyReject) pageReadyReject(new Error('录屏页面加载超时'))
      }, 10000)

      // 加载录屏页面
      const isDev = !app.isPackaged
      if (isDev) {
        await recorderWin.loadURL('http://localhost:5173/#/recorder')
      } else {
        await recorderWin.loadFile(join(__dirname, '../renderer/index.html'), { hash: '/recorder' })
      }

      log.info('[Recorder] Page loaded, waiting for ready...')

      // 等待页面就绪信号
      try {
        await pageReadyPromise
        clearTimeout(loadTimeout)
        ipcMain.removeListener(IPC_CHANNELS['overlay:ready'], readyHandler)
      } catch (e: any) {
        this.cleanup()
        isStarting = false
        return { success: false, error: e.message }
      }

      // 通知录屏页面开始
      log.info('[Recorder] Signaling page to start recording')
      recorderWin.webContents.send(IPC_CHANNELS['recorder:pageStart'], { fps, mic })

      // 等待录屏页面确认已开始
      let startedResolve: ((started: boolean) => void) | null = null
      const startedPromise = new Promise<boolean>((resolve) => {
        startedResolve = resolve
      })

      const startedHandler = () => {
        log.info('[Recorder] Page confirmed recording started')
        if (startedResolve) startedResolve(true)
      }
      ipcMain.on(IPC_CHANNELS['recorder:started'], startedHandler)

      const startedTimeout = setTimeout(() => {
        ipcMain.removeListener(IPC_CHANNELS['recorder:started'], startedHandler)
        if (startedResolve) startedResolve(false)
      }, 15000)

      const started = await startedPromise
      clearTimeout(startedTimeout)
      ipcMain.removeListener(IPC_CHANNELS['recorder:started'], startedHandler)

      if (!started) {
        this.cleanup()
        isStarting = false
        return { success: false, error: '录屏启动失败: 页面未响应' }
      }

      isStarting = false
      isRecording = true
      recordStartTime = Date.now()

      // 计时器: 每秒广播状态
      elapsedTimer = setInterval(() => {
        const status = this.getStatus()
        WindowManager.broadcast(IPC_CHANNELS['recorder:statusChanged'], status)
      }, 1000)

      log.info('[Recorder] Recording started')
      return { success: true }

    } catch (e: any) {
      log.error('[Recorder] Start failed:', e.message)
      this.cleanup()
      isStarting = false
      return { success: false, error: e.message }
    }
  },

  async stop(): Promise<{ success: boolean; filepath?: string; error?: string }> {
    if (!isRecording || !recorderWin) {
      return { success: false, error: '没有正在进行的录屏' }
    }

    return new Promise((resolve) => {
      let resolved = false

      const safeResolve = (result: { success: boolean; filepath?: string; error?: string }) => {
        if (resolved) return
        resolved = true
        resolve(result)
      }

      // 设置超时
      const timeout = setTimeout(() => {
        log.error('[Recorder] Stop timeout')
        this.cleanup()
        safeResolve({ success: false, error: '录屏停止超时' })
      }, 15000)

      // 监听完成事件
      const completeHandler = (_e: any, data: ArrayBuffer, mimeType: string) => {
        clearTimeout(timeout)
        ipcMain.removeListener(IPC_CHANNELS['recorder:complete'], completeHandler)

        try {
          const cfg = ConfigService.get()
          const dir = cfg.recorder.dir
          if (!existsSync(dir)) mkdirSync(dir, { recursive: true })

          const now = new Date()
          const ts = `${now.getFullYear()}${pad(now.getMonth()+1)}${pad(now.getDate())}_${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`
          const ext = mimeType.includes('mp4') ? 'mp4' : 'webm'
          const filename = `seewo-record-${ts}.${ext}`
          const filepath = join(dir, filename)

          writeFileSync(filepath, Buffer.from(data))

          const duration = Math.round((Date.now() - recordStartTime) / 1000)
          log.info(`[Recorder] Saved: ${filepath} (${duration}s, ${(data.byteLength / 1024 / 1024).toFixed(1)}MB)`)

          // 通知
          if (Notification.isSupported()) {
            new Notification({
              title: '录屏已保存',
              body: `时长 ${duration}s - ${filename}`,
              timeoutType: 'default'
            }).show()
          }

          this.cleanup()
          safeResolve({ success: true, filepath })
        } catch (err: any) {
          log.error('[Recorder] Save failed:', err.message)
          this.cleanup()
          safeResolve({ success: false, error: err.message })
        }
      }

      ipcMain.on(IPC_CHANNELS['recorder:complete'], completeHandler)

      // 通知录屏页面停止
      if (recorderWin && !recorderWin.isDestroyed()) {
        recorderWin.webContents.send(IPC_CHANNELS['recorder:pageStop'])
      }
    })
  },

  cleanup(): void {
    if (elapsedTimer) {
      clearInterval(elapsedTimer)
      elapsedTimer = null
    }

    isRecording = false
    isStarting = false
    recordStartTime = 0

    // 广播停止状态
    WindowManager.broadcast(IPC_CHANNELS['recorder:statusChanged'], this.getStatus())

    if (recorderWin && !recorderWin.isDestroyed()) {
      recorderWin.destroy()
    }
    recorderWin = null

    log.info('[Recorder] Cleaned up')
  }
}

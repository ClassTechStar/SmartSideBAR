// src/main/main.ts - 主进程入口 (完整版 v2.1 - 真实录屏/批注/区域截图)

import { app, BrowserWindow, ipcMain, screen, globalShortcut, shell, dialog, Notification } from 'electron'
import { join } from 'path'
import { spawn } from 'child_process'
import log from 'electron-log'

import { ConfigService } from './services/config'
import { DisplayService } from './services/display'
import { ImeService } from './services/ime'
import { UsbService } from './services/usb'
import { PrinterService } from './services/printer'
import { SchedulerService } from './services/scheduler'
import { CaptureService } from './services/capture'
import { RecorderService } from './services/recorder'
import { DiagnosticService } from './services/diagnostic'
import { LongshotService } from './services/longshot'
import { WindowManager } from './windows/manager'
import { IPC_CHANNELS, isAllowedChannel } from '../shared/ipc-channels'
import type { SidekickConfig, ReminderSoundConfig } from '../shared/types'

// 单例锁
const gotLock = app.requestSingleInstanceLock()
if (!gotLock) {
  app.quit()
  process.exit(0)
}

app.on('second-instance', () => {
  WindowManager.focusMain()
})

// 日志配置
log.transports.file.resolvePathFn = () => {
  return join(app.getPath('userData'), 'logs', `main-${new Date().toISOString().slice(0, 10)}.log`)
}

// 禁用未用特性降低内存
app.commandLine.appendSwitch('disable-features', 'AutofillServerCommunication,Translate')

let config: SidekickConfig | null = null

// 当前活跃的 overlay/annotator 窗口引用
let activeOverlayWin: BrowserWindow | null = null
let activeAnnotatorWin: BrowserWindow | null = null

async function bootstrap() {
  log.info('[BOOT] Seewo Sidekick v2.1.0 starting...')

  try {
    // ① 配置先行
    config = await ConfigService.init()
    log.info('[BOOT] Config loaded')

    // ② 显示服务
    await DisplayService.init(config)
    log.info('[BOOT] Display service ready')

    // ③ 服务启动
    await ImeService.start().catch(e => log.warn('[BOOT] ImeService degraded:', e.message))
    await UsbService.start(config).catch(e => log.warn('[BOOT] UsbService degraded:', e.message))
    await PrinterService.start(config).catch(e => log.warn('[BOOT] PrinterService degraded:', e.message))
    await SchedulerService.start().catch(e => log.warn('[BOOT] SchedulerService degraded:', e.message))

    // ④ 窗口管理
    const mainWin = WindowManager.createSidebar(config)

    // ⑤ OOBE 判定
    if (!config.oobe.completed && !config.oobe.skipped) {
      WindowManager.createOobe(mainWin)
      log.info('[BOOT] OOBE window created')
    } else {
      mainWin.show()
      log.info('[BOOT] Main window shown')
    }

    // ⑥ 诊断服务就绪
    log.info('[BOOT] Diagnostic service ready')

    // ⑦ 显示器/DPI 变更监听 → 防抖后自动重定位侧边栏
    // 等待 Electron 显示器列表更新完成; 合并连续事件, 避免引用已消失的显示器
    let displayChangeTimer: NodeJS.Timeout | null = null
    const onDisplayEvent = (reason: string) => {
      if (displayChangeTimer) clearTimeout(displayChangeTimer)
      displayChangeTimer = setTimeout(() => {
        log.info(`[Display] ${reason}, repositioning sidebar`)
        WindowManager.onDisplayChanged()
      }, 250)
    }
    screen.on('display-metrics-changed', () => onDisplayEvent('Metrics changed'))
    screen.on('display-added', () => onDisplayEvent('Display added'))
    screen.on('display-removed', () => onDisplayEvent('Display removed'))

    // ⑧ 注册全局截图热键 (跨显示器场景: 热键动作吸附到当前目标显示器)
    registerCaptureHotkey(config)
  } catch (e) {
    log.error('[BOOT] Fatal bootstrap error:', e)
    app.quit()
  }
}

// ================= IPC 通道注册 =================

// ---- 配置 ----
ipcMain.handle(IPC_CHANNELS['config:get'], async () => ConfigService.get())
ipcMain.handle(IPC_CHANNELS['config:set'], async (_event, key: string, value: unknown) => {
  const result = ConfigService.set(key, value)
  // 热键变更时动态重注册
  if (key === 'capture.hotkey' && config) {
    registerCaptureHotkey(config)
  }
  WindowManager.broadcast(IPC_CHANNELS['config:updated'], ConfigService.get())
  return result
})

// ---- IME ----
ipcMain.handle(IPC_CHANNELS['ime:getState'], async () => ImeService.getState())
ipcMain.handle(IPC_CHANNELS['ime:toggle'], async () => {
  const state = await ImeService.toggle()
  WindowManager.broadcast(IPC_CHANNELS['ime:changed'], state)
  return state
})

// ---- 截图: 区域截图 ----
async function runRegionCapture(opts?: any): Promise<{ success: boolean; filepath?: string; error?: string }> {
  const mode = opts?.mode || 'region'

  // 隐藏侧边栏
  WindowManager.hideMain()
  await sleep(300)

  // 截目标显示器 (侧边栏所在屏; 显示器切换/拔出后自动回退到有效屏)
  const shot = await CaptureService.grabTarget()
  const img = shot?.img || null
  if (!img) {
    WindowManager.showMain()
    return { success: false, error: '无法获取屏幕图像' }
  }

  // 全屏截图模式 (无区域选择)
  if (mode === 'fullscreen') {
    const filepath = await CaptureService.saveImage(img)
    WindowManager.showMain()
    if (filepath && Notification.isSupported()) {
      new Notification({ title: '截图已保存', body: filepath, timeoutType: 'default' }).show()
    }
    return { success: !!filepath, filepath: filepath || undefined }
  }

  // 长截图已迁移到专用 IPC 通道
  if (mode === 'longshot') {
    WindowManager.showMain()
    return { success: false, error: '请使用新版长截图功能（点击「长截图」按钮）' }
  }

  // 区域截图: 打开 overlay 让用户选择
  const tmpPath = CaptureService.saveTempImage(img)
  const target = shot?.target || DisplayService.sidebarTarget()
  const scaleFactor = target.scaleFactor
  // 截图源对应的显示器 workArea (任务栏在上/左时 overlay 原点 ≠ 屏幕原点, 裁剪需补偏移)
  const shotWorkArea = shot?.workArea || target.workArea
  const imgSize = img.getSize()

  activeOverlayWin = WindowManager.showOverlay()

  if (!activeOverlayWin) {
    WindowManager.showMain()
    return { success: false, error: '无法创建选择窗口' }
  }

  // 等待 overlay 就绪, 然后发送初始化数据
  const result = await new Promise<{ success: boolean; filepath?: string; error?: string }>((resolve) => {
    let resolved = false

    // overlay 就绪
    const readyHandler = () => {
      if (resolved) return
      log.info('[Overlay] Ready, sending init')
      activeOverlayWin?.webContents.send(IPC_CHANNELS['overlay:init'], {
        mode: 'region',
        screenshotPath: tmpPath,
        scaleFactor,
        screenWidth: imgSize.width,
        screenHeight: imgSize.height,
        dipWidth: shotWorkArea.width,
        dipHeight: shotWorkArea.height
      })
    }
    ipcMain.on(IPC_CHANNELS['overlay:ready'], readyHandler)

    // overlay 窗口被外部关闭 (Alt+F4 / 系统关机) 时兜底, 避免 Promise 悬挂 + 引用泄漏
    const winForClose = activeOverlayWin
    const closedHandler = () => {
      if (resolved) return
      log.warn('[Overlay] Window closed externally, aborting capture')
      resolved = true
      cleanup()
      CaptureService.cleanupTemp(tmpPath)
      activeOverlayWin = null
      WindowManager.showMain()
      resolve({ success: false, error: '选择窗口已关闭' })
    }
    winForClose?.on('closed', closedHandler)

    // 用户选择区域
    const regionHandler = async (_e: any, region: { x: number; y: number; width: number; height: number }) => {
      if (resolved) return
      resolved = true
      cleanup()

      log.info('[Overlay] Region selected:', region)

      // region 坐标是 overlay 内 DIP, 需加上截图源显示器 workArea 原点偏移后乘 scaleFactor 转物理像素
      const physRegion = {
        x: Math.round((region.x + shotWorkArea.x) * scaleFactor),
        y: Math.round((region.y + shotWorkArea.y) * scaleFactor),
        width: Math.round(region.width * scaleFactor),
        height: Math.round(region.height * scaleFactor)
      }

      const filepath = await CaptureService.saveImage(img, physRegion)
      CaptureService.cleanupTemp(tmpPath)

      if (activeOverlayWin && !activeOverlayWin.isDestroyed()) {
        activeOverlayWin.close()
        activeOverlayWin = null
      }
      WindowManager.showMain()

      if (filepath && Notification.isSupported()) {
        new Notification({ title: '区域截图已保存', body: filepath, timeoutType: 'default' }).show()
      }

      resolve({ success: !!filepath, filepath: filepath || undefined })
    }
    ipcMain.on(IPC_CHANNELS['overlay:region'], regionHandler)

    // 用户取消
    const cancelHandler = () => {
      if (resolved) return
      resolved = true
      cleanup()
      log.info('[Overlay] Cancelled')
      CaptureService.cleanupTemp(tmpPath)
      if (activeOverlayWin && !activeOverlayWin.isDestroyed()) {
        activeOverlayWin.close()
        activeOverlayWin = null
      }
      WindowManager.showMain()
      resolve({ success: false, error: '用户取消' })
    }
    ipcMain.on(IPC_CHANNELS['overlay:cancel'], cancelHandler)

    function cleanup() {
      ipcMain.removeListener(IPC_CHANNELS['overlay:ready'], readyHandler)
      ipcMain.removeListener(IPC_CHANNELS['overlay:region'], regionHandler)
      ipcMain.removeListener(IPC_CHANNELS['overlay:cancel'], cancelHandler)
      if (winForClose) winForClose.removeListener('closed', closedHandler)
    }

    // 超时保护 (60s)
    setTimeout(() => {
      if (resolved) return
      resolved = true
      cleanup()
      log.warn('[Overlay] Timeout')
      CaptureService.cleanupTemp(tmpPath)
      if (activeOverlayWin && !activeOverlayWin.isDestroyed()) {
        activeOverlayWin.close()
        activeOverlayWin = null
      }
      WindowManager.showMain()
      resolve({ success: false, error: '操作超时' })
    }, 60000)
  })

  return result
}

ipcMain.handle(IPC_CHANNELS['capture:region'], async (_event, opts) => {
  log.info('[IPC] capture:region', opts)
  return runRegionCapture(opts)
})

// 全局截图热键 (目标显示器 = 侧边栏吸附屏; 显示器热插拔后由 sidebarTarget 自动重算)
function registerCaptureHotkey(cfg: SidekickConfig): void {
  const hotkey = cfg.capture?.hotkey
  if (!hotkey) return
  try {
    // 先注销旧绑定, 避免重复注册 / 同键冲突
    globalShortcut.unregister(hotkey)
    const ok = globalShortcut.register(hotkey, () => {
      log.info(`[Hotkey] Global capture hotkey triggered: ${hotkey}`)
      runRegionCapture({ mode: 'region' }).catch(e => log.error('[Hotkey] capture failed:', e))
    })
    if (ok) {
      log.info(`[Hotkey] Registered: ${hotkey}`)
    } else {
      log.warn(`[Hotkey] Register failed (可能已被其他应用占用): ${hotkey}`)
    }
  } catch (e: any) {
    log.warn(`[Hotkey] Register error: ${e.message}`)
  }
}

// ---- 长截图 ----
ipcMain.handle(IPC_CHANNELS['longshot:selectWindow'], async () => {
  log.info('[IPC] longshot:selectWindow')
  const result = await LongshotService.listWindows()
  return result
})

ipcMain.handle(IPC_CHANNELS['longshot:start'], async (_event, opts?: { window?: any }) => {
  log.info('[IPC] longshot:start opts=', opts)
  WindowManager.hideMain()
  await sleep(500)
  const filepath = await LongshotService.start(opts)
  WindowManager.showMain()
  if (filepath && Notification.isSupported()) {
    new Notification({ title: '长截图已保存', body: filepath, timeoutType: 'default' }).show()
  }
  return { success: !!filepath, filepath: filepath || undefined, error: filepath ? undefined : '长截图失败' }
})

ipcMain.handle(IPC_CHANNELS['longshot:stop'], async () => {
  log.info('[IPC] longshot:stop')
  LongshotService.stop()
  return { success: true }
})

// ---- 截图: 批注 ----
// 透明屏幕绘图模式: 直接在真实屏幕上绘图, 不抓背景截图, 秒开秒画, 更直觉
ipcMain.handle(IPC_CHANNELS['capture:annotate'], async () => {
  log.info('[IPC] capture:annotate (transparent overlay mode)')

  WindowManager.hideMain()
  await sleep(300)

  const target = DisplayService.sidebarTarget()
  const scaleFactor = target.scaleFactor

  activeAnnotatorWin = WindowManager.showAnnotator()

  if (!activeAnnotatorWin) {
    WindowManager.showMain()
    return { success: false, error: '无法创建批注窗口' }
  }

  const result = await new Promise<{ success: boolean; filepath?: string; error?: string }>((resolve) => {
    let resolved = false

    const readyHandler = () => {
      if (resolved) return
      log.info('[Annotator] Ready, sending init (transparent mode)')
      activeAnnotatorWin?.webContents.send(IPC_CHANNELS['overlay:init'], {
        mode: 'annotate',
        transparent: true,
        scaleFactor,
        dipWidth: target.workArea.width,
        dipHeight: target.workArea.height
      })
    }
    ipcMain.on(IPC_CHANNELS['overlay:ready'], readyHandler)

    // 批注窗口被外部关闭 (Alt+F4 / 系统关机) 时兜底, 避免 Promise 悬挂 + 引用泄漏
    const winForClose = activeAnnotatorWin
    const closedHandler = () => {
      if (resolved) return
      log.warn('[Annotator] Window closed externally, aborting annotate')
      resolved = true
      cleanup()
      activeAnnotatorWin = null
      WindowManager.showMain()
      resolve({ success: false, error: '批注窗口已关闭' })
    }
    winForClose?.on('closed', closedHandler)

    const saveHandler = (_e: any, dataUrl: string) => {
      if (resolved) return
      resolved = true
      cleanup()

      log.info('[Annotator] Save received')
      const filepath = CaptureService.saveAnnotatedImage(dataUrl)

      if (activeAnnotatorWin && !activeAnnotatorWin.isDestroyed()) {
        activeAnnotatorWin.close()
        activeAnnotatorWin = null
      }
      WindowManager.showMain()

      if (filepath && Notification.isSupported()) {
        new Notification({ title: '批注已保存', body: filepath, timeoutType: 'default' }).show()
      }

      resolve({ success: !!filepath, filepath: filepath || undefined })
    }
    ipcMain.on(IPC_CHANNELS['overlay:saveAnnotate'], saveHandler)

    const cancelHandler = () => {
      if (resolved) return
      resolved = true
      cleanup()
      log.info('[Annotator] Cancelled')
      if (activeAnnotatorWin && !activeAnnotatorWin.isDestroyed()) {
        activeAnnotatorWin.close()
        activeAnnotatorWin = null
      }
      WindowManager.showMain()
      resolve({ success: false, error: '用户取消' })
    }
    ipcMain.on(IPC_CHANNELS['overlay:cancel'], cancelHandler)

    function cleanup() {
      ipcMain.removeListener(IPC_CHANNELS['overlay:ready'], readyHandler)
      ipcMain.removeListener(IPC_CHANNELS['overlay:saveAnnotate'], saveHandler)
      ipcMain.removeListener(IPC_CHANNELS['overlay:cancel'], cancelHandler)
      if (winForClose) winForClose.removeListener('closed', closedHandler)
    }

    // 超时保护 (5min)
    setTimeout(() => {
      if (resolved) return
      resolved = true
      cleanup()
      if (activeAnnotatorWin && !activeAnnotatorWin.isDestroyed()) {
        activeAnnotatorWin.close()
        activeAnnotatorWin = null
      }
      WindowManager.showMain()
      resolve({ success: false, error: '操作超时' })
    }, 300000)
  })

  return result
})

// ---- 录屏 ----
ipcMain.handle(IPC_CHANNELS['recorder:start'], async (_event, opts) => {
  log.info('[IPC] recorder:start', opts)
  return RecorderService.start(opts)
})

ipcMain.handle(IPC_CHANNELS['recorder:stop'], async () => {
  log.info('[IPC] recorder:stop')
  return RecorderService.stop()
})

ipcMain.handle(IPC_CHANNELS['recorder:status'], async () => {
  return RecorderService.getStatus()
})

// ---- USB ----
ipcMain.handle(IPC_CHANNELS['usb:list'], async () => {
  return UsbService.list()
})

ipcMain.handle(IPC_CHANNELS['usb:scan'], async () => {
  log.info('[IPC] usb:scan (manual refresh)')
  const drives = await UsbService.scan()
  return { success: true, drives }
})

// ---- 打印机 ----
ipcMain.handle(IPC_CHANNELS['printer:status'], async () => {
  return PrinterService.getStatus()
})

// ---- 显示 ----
ipcMain.handle(IPC_CHANNELS['display:list'], async () => DisplayService.list())
ipcMain.handle(IPC_CHANNELS['display:sidebarTarget'], async () => DisplayService.sidebarTarget())

// ---- 任务管理器 ----
ipcMain.handle(IPC_CHANNELS['app:openTaskMgr'], async () => {
  try {
    spawn('taskmgr.exe', { detached: true })
    return true
  } catch (e) {
    log.error('[IPC] Open taskmgr failed:', e)
    try {
      spawn('cmd', ['/c', 'start', 'taskmgr'])
      return true
    } catch { /* ignore */ }
    return false
  }
})

// ---- 系统 Shell ----
ipcMain.handle(IPC_CHANNELS['shell:openExternal'], async (_event, url: string) => {
  await shell.openExternal(url)
})
ipcMain.handle(IPC_CHANNELS['shell:openPath'], async (_event, filePath: string) => {
  const result = await shell.openPath(filePath)
  return result === ''
})
ipcMain.handle(IPC_CHANNELS['shell:showItemInFolder'], async (_event, filePath: string) => {
  shell.showItemInFolder(filePath)
  return true
})

// ---- OOBE ----
ipcMain.handle(IPC_CHANNELS['oobe:getState'], async () => ConfigService.get().oobe)
ipcMain.handle(IPC_CHANNELS['oobe:setState'], async (_event, state) => {
  return ConfigService.set('oobe', state)
})
ipcMain.handle(IPC_CHANNELS['oobe:closeAndOpenMain'], async () => {
  WindowManager.closeOobe()
  WindowManager.showMain()
  return true
})

// ---- 电源 ----
ipcMain.handle(IPC_CHANNELS['power:setAutoLaunch'], async (_event, enabled: boolean) => {
  app.setLoginItemSettings({ openAtLogin: enabled })
  return true
})
ipcMain.handle(IPC_CHANNELS['power:getAutoLaunch'], async () => {
  return app.getLoginItemSettings().openAtLogin
})

// ---- 窗口 ----
ipcMain.handle(IPC_CHANNELS['window:show'], async () => {
  WindowManager.showMain()
  return true
})
ipcMain.handle(IPC_CHANNELS['window:hide'], async () => {
  WindowManager.hideMain()
  return true
})
ipcMain.handle(IPC_CHANNELS['window:minimize'], async () => {
  WindowManager.minimize()
  return true
})
ipcMain.handle(IPC_CHANNELS['window:resize'], async (_event, width: number, height: number) => {
  WindowManager.resizeMain(width, height)
  return true
})
ipcMain.handle(IPC_CHANNELS['window:openSettings'], async () => {
  WindowManager.showSettings()
  return true
})
ipcMain.handle(IPC_CHANNELS['window:dock'], async () => {
  WindowManager.dockMain()
  return true
})
ipcMain.handle(IPC_CHANNELS['window:undock'], async () => {
  WindowManager.undockMain()
  return true
})

// ---- 通知 ----
ipcMain.handle(IPC_CHANNELS['notification:show'], async (_event, item: any) => {
  if (Notification.isSupported()) {
    const n = new Notification({
      title: item.title || '希沃侧边快捷键工具',
      body: item.message || '',
      timeoutType: item.duration > 0 ? 'default' : 'never'
    })
    n.show()
  }
  return true
})
ipcMain.handle(IPC_CHANNELS['notification:dismiss'], async () => {
  return true
})

// ---- 提醒 ----
ipcMain.handle(IPC_CHANNELS['reminder:add'], async (_event, r: any) => {
  SchedulerService.add(r)
  return true
})
ipcMain.handle(IPC_CHANNELS['reminder:remove'], async (_event, id: string) => {
  SchedulerService.remove(id)
  return true
})
ipcMain.handle(IPC_CHANNELS['reminder:list'], async () => {
  return SchedulerService.list()
})
// 选择 MP3 铃声文件
ipcMain.handle(IPC_CHANNELS['reminder:selectSound'], async () => {
  const result = await dialog.showOpenDialog({
    title: '选择提醒铃声',
    filters: [{ name: '音频文件', extensions: ['mp3', 'wav', 'ogg', 'm4a'] }],
    properties: ['openFile']
  })
  if (result.canceled || result.filePaths.length === 0) return null
  const selectedPath = result.filePaths[0]
  ConfigService.set('reminderSound.mp3Path', selectedPath)
  // 同时切换到 custom 预设
  ConfigService.set('reminderSound.preset', 'custom')
  log.info('[Reminder] Selected sound file:', selectedPath)
  return selectedPath
})
// 测试播放铃声
ipcMain.handle(IPC_CHANNELS['reminder:playTest'], async (_event, soundConfig?: ReminderSoundConfig) => {
  const cfg = soundConfig || ConfigService.get().reminderSound
  SchedulerService.playSound(cfg)
  return true
})

// ---- 帮助 / 诊断 ----
ipcMain.handle(IPC_CHANNELS['help:runDiagnostics'], async () => {
  log.info('[IPC] help:runDiagnostics')
  const result = await DiagnosticService.runFull()
  return result
})

// 诊断: 实时诊断信息
ipcMain.handle(IPC_CHANNELS['diag:getStatus'], async () => {
  return {
    usb: UsbService.getDiagnostics?.() || {},
    recorder: RecorderService.getStatus(),
    memory: process.memoryUsage(),
    uptime: process.uptime(),
    displays: DisplayService.list()
  }
})

ipcMain.handle(IPC_CHANNELS['help:exportDiagPack'], async () => {
  log.info('[IPC] help:exportDiagPack')
  const result = await DiagnosticService.runFull()
  const packPath = await DiagnosticService.exportPack(result)
  return packPath
})

// 安全: 拦截未在白名单的 IPC 调用
ipcMain.on('message', (event, channel) => {
  if (!isAllowedChannel(channel)) {
    log.warn(`[SECURITY] Blocked unauthorized IPC channel: ${channel}`)
    event.preventDefault()
  }
})

// 辅助函数
function sleep(ms: number): Promise<void> {
  return new Promise(r => setTimeout(r, ms))
}

// ================= 启动 =================
app.whenReady().then(bootstrap)

// 退出清理
app.on('before-quit', () => {
  globalShortcut.unregisterAll()
  SchedulerService.stop()
  UsbService.stop()
  PrinterService.stop()
  ImeService.stop()
  RecorderService.cleanup()
})

app.on('window-all-closed', () => {
  // Windows 下保持后台运行
})

// 崩溃恢复
app.on('render-process-gone', (_event, _webContents, details) => {
  log.error(`[CRASH] Render process gone: ${details.reason}`)
  setTimeout(() => {
    WindowManager.recreateMain(config!)
  }, 3000)
})

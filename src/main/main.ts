// src/main/main.ts - 主进程入口 (完整版 v2.1 - 真实录屏/批注/区域截图)

import { app, BrowserWindow, ipcMain, screen, shell, dialog, Notification } from 'electron'
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
import { TrayService } from './services/tray'
import { HotkeyService } from './services/hotkey'
import { WindowManager } from './windows/manager'
import { IPC_CHANNELS } from '../shared/ipc-channels'
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

// 设置 AppUserModelId，确保 Windows 任务栏图标分组与通知归属正确
app.setAppUserModelId('com.seewo.smartsidebar')

let config: SidekickConfig | null = null

// 当前活跃的 overlay/annotator 窗口引用
let activeOverlayWin: BrowserWindow | null = null
let activeAnnotatorWin: BrowserWindow | null = null

async function bootstrap() {
  log.info('[BOOT] Seewo Sidekick v1.1.0 starting...')

  try {
    // ① 配置先行
    config = await ConfigService.init()
    log.info('[BOOT] Config loaded')

    // ② 显示服务
    await DisplayService.init(config)
    log.info('[BOOT] Display service ready')

    // ③ 服务启动
    // P2-1: 白名单策略落地 —— policy.disabledModules 中被点名的模块,
    // 其常驻服务不启动 (UI 侧由 SidebarApp 同步隐藏对应 rail 图标)。
    // 合法模块名: ime / usb / printer / reminder (capture/links/taskmgr 为 IPC 触发, 无常驻服务)
    const disabled = (m: string) => ConfigService.isModuleDisabled(m)
    if (disabled('ime')) log.info('[BOOT] Module "ime" disabled by policy, skipping ImeService')
    else await ImeService.start().catch(e => log.warn('[BOOT] ImeService degraded:', e.message))
    if (disabled('usb')) log.info('[BOOT] Module "usb" disabled by policy, skipping UsbService')
    else await UsbService.start(config).catch(e => log.warn('[BOOT] UsbService degraded:', e.message))
    if (disabled('printer')) log.info('[BOOT] Module "printer" disabled by policy, skipping PrinterService')
    else await PrinterService.start(config).catch(e => log.warn('[BOOT] PrinterService degraded:', e.message))
    if (disabled('reminder')) log.info('[BOOT] Module "reminder" disabled by policy, skipping SchedulerService')
    else await SchedulerService.start().catch(e => log.warn('[BOOT] SchedulerService degraded:', e.message))

    // ④ 窗口管理
    const mainWin = WindowManager.createSidebar(config)

    // ⑤ OOBE 判定
    if (!config.oobe.completed && !config.oobe.skipped) {
      // P0-1 修复: OOBE 为独立置顶窗口 (不再 modal 挂在未 show 的侧边栏上);
      // 「开始使用/跳过」由 OobeApp 走 oobe:closeAndOpenMain → showMain。
      const oobeWin = WindowManager.createOobe()
      log.info('[BOOT] OOBE window created')

      // P0-1 兜底: 用户直接关闭 OOBE 窗口 (Alt+F4 / 系统关机) 且未完成/未跳过时,
      // 侧边栏仍需可见 —— 否则应用表现为「启动了但什么都没有」。
      oobeWin.on('closed', () => {
        const oobe = ConfigService.get().oobe
        if (!oobe.completed && !oobe.skipped) {
          log.info('[BOOT] OOBE closed without completion, showing sidebar')
          WindowManager.showMain()
        }
      })
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
        // E7 修复: 统一由此处防抖后刷新 DisplayService 缓存再重定位侧边栏
        DisplayService.refresh(config!)
        WindowManager.onDisplayChanged()
      }, 250)
    }
    screen.on('display-metrics-changed', () => onDisplayEvent('Metrics changed'))
    screen.on('display-added', () => onDisplayEvent('Display added'))
    screen.on('display-removed', () => onDisplayEvent('Display removed'))

    // ⑧ 注册全局截图热键 (跨显示器场景: 热键动作吸附到当前目标显示器)
    registerAllHotkeys(config)

    // ⑨ 系统托盘 (F10: 退出入口 + 通知归属)
    TrayService.init()
  } catch (e) {
    log.error('[BOOT] Fatal bootstrap error:', e)
    app.quit()
  }
}

// ================= IPC 通道注册 =================

// ---- 配置 ----
// P0-6/B3: config:set 加键名白名单 — 阻止渲染层写入非预期配置字段或原型链路径
const ALLOWED_CONFIG_KEYS = new Set([
  'version', 'ime', 'capture', 'recorder', 'usb', 'printer',
  'display', 'links', 'reminders', 'reminderSound', 'oobe', 'policy',
  'capture.hotkey', 'capture.annotateHotkey', 'capture.longshotHotkey', 'capture.format', 'capture.dir',
  'recorder.fps', 'recorder.bitrate', 'recorder.mic', 'recorder.dir',
  'usb.enabled', 'usb.ignoreTypes',
  'printer.pollIntervalSec',
  'display.sidebarMonitor', 'display.sidebarSide', 'display.fitWindowsToWorkArea',
  'reminderSound.preset', 'reminderSound.mp3Path', 'reminderSound.volume',
  'reminderSound.repeat', 'reminderSound.repeatInterval',
])
ipcMain.handle(IPC_CHANNELS['config:get'], async () => ConfigService.get())
ipcMain.handle(IPC_CHANNELS['config:set'], async (_event, key: string, value: unknown) => {
  // P0-6/B3: 校验 key 格式 (仅字母/数字/点/下划线, 阻止 __proto__/constructor 等)
  if (typeof key !== 'string' || !/^[a-zA-Z][a-zA-Z0-9._]*$/.test(key)) {
    log.warn(`[SECURITY] config:set rejected: invalid key format "${key}"`)
    return false
  }
  // 点分路径取顶级键, 校验是否在白名单内
  const topKey = key.split('.')[0]
  if (!ALLOWED_CONFIG_KEYS.has(key) && !ALLOWED_CONFIG_KEYS.has(topKey)) {
    log.warn(`[SECURITY] config:set rejected: key "${key}" not in whitelist`)
    return false
  }
  const result = ConfigService.set(key, value)
  // 热键变更时动态重注册 (P2-2: 3 个槽位)
  if (config && (key === 'capture.hotkey' || key === 'capture.annotateHotkey' || key === 'capture.longshotHotkey')) {
    registerAllHotkeys(config)
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
  // C4 修复: 原实现先把整屏 PNG 写入临时目录 (saveTempImage), 但 OverlayApp 的
  // onInit 回调体为空, screenshotPath 从不被消费 —— 每次截图多一次全屏 PNG 编码 + 磁盘 I/O。
  // overlay 只负责框选坐标 (DIP), 裁剪直接用内存中的 img, 无需落盘临时文件。
  const target = shot?.target || DisplayService.sidebarTarget()
  const scaleFactor = target.scaleFactor
  // 截图源对应的显示器 workArea (任务栏在上/左时 overlay 原点 ≠ 屏幕原点, 裁剪需补偏移)
  const shotWorkArea = shot?.workArea || target.workArea
  // P0-3: 源显示器 bounds —— desktopCapturer 源图像以 bounds 原点为 (0,0),
  // 副屏 (bounds.x/y ≠ 0) 时必须扣除, 否则裁剪区域整体偏移一个屏宽/高
  const shotBounds = shot?.bounds || target.bounds
  const imgSize = img.getSize()
  // thumbnailSize 上限 3840x2160: 物理分辨率超过上限的屏 (如 5K) 源图像会被等比缩小,
  // 物理像素 → 图像像素需再乘该比例; 常规屏 (≤4K) 时恒为 1
  const physWidth = shotBounds.width * scaleFactor
  const physHeight = shotBounds.height * scaleFactor
  const imgScaleX = physWidth > 0 ? imgSize.width / physWidth : 1
  const imgScaleY = physHeight > 0 ? imgSize.height / physHeight : 1

  // 等待 overlay 就绪, 然后发送初始化数据
  const result = await new Promise<{ success: boolean; filepath?: string; error?: string }>((resolve) => {
    let resolved = false

    // overlay 就绪
    const readyHandler = () => {
      if (resolved) return
      log.info('[Overlay] Ready, sending init')
      activeOverlayWin?.webContents.send(IPC_CHANNELS['overlay:init'], {
        mode: 'region',
        scaleFactor,
        screenWidth: imgSize.width,
        screenHeight: imgSize.height,
        dipWidth: shotWorkArea.width,
        dipHeight: shotWorkArea.height
      })
    }
    // E9 修复: 先注册 overlay:ready 监听, 再创建窗口加载页面, 避免竞态丢信号
    ipcMain.on(IPC_CHANNELS['overlay:ready'], readyHandler)

    activeOverlayWin = WindowManager.showOverlay()

    if (!activeOverlayWin) {
      ipcMain.removeListener(IPC_CHANNELS['overlay:ready'], readyHandler)
      WindowManager.showMain()
      resolve({ success: false, error: '无法创建选择窗口' })
      return
    }

    // overlay 窗口被外部关闭 (Alt+F4 / 系统关机) 时兜底, 避免 Promise 悬挂 + 引用泄漏
    const winForClose = activeOverlayWin
    const closedHandler = () => {
      if (resolved) return
      log.warn('[Overlay] Window closed externally, aborting capture')
      resolved = true
      cleanup()
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

      // P0-3 修复: region 是 overlay 窗口内 DIP (overlay 原点 = workArea 原点)。
      // 源图像坐标系 = 该显示器 bounds 原点为 (0,0) 的物理像素,
      // 故: 图像x = (region.x + workArea.x - bounds.x) × scaleFactor × imgScale。
      // 原实现漏减 bounds.x/y —— 主屏 (bounds 原点 0,0) 碰巧正确, 副屏整体偏移一个屏。
      const physRegion = {
        x: Math.round((region.x + shotWorkArea.x - shotBounds.x) * scaleFactor * imgScaleX),
        y: Math.round((region.y + shotWorkArea.y - shotBounds.y) * scaleFactor * imgScaleY),
        width: Math.round(region.width * scaleFactor * imgScaleX),
        height: Math.round(region.height * scaleFactor * imgScaleY)
      }

      const filepath = await CaptureService.saveImage(img, physRegion)

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

// P2-2: 3 个全局热键槽位 (截图/批注/长截图), 由 HotkeyService 统一管理。
// 注册失败 (被其他应用占用) 时 HotkeyService 会记录替代建议, 供设置 UI 提示。
function registerAllHotkeys(cfg: SidekickConfig): void {
  const c = cfg.capture || ({} as SidekickConfig['capture'])
  HotkeyService.register('capture', c.hotkey, () => {
    log.info('[Hotkey] capture triggered')
    runRegionCapture({ mode: 'region' }).catch(e => log.error('[Hotkey] capture failed:', e))
  })
  HotkeyService.register('annotate', c.annotateHotkey, () => {
    log.info('[Hotkey] annotate triggered')
    runAnnotate().catch(e => log.error('[Hotkey] annotate failed:', e))
  })
  HotkeyService.register('longshot', c.longshotHotkey, () => {
    log.info('[Hotkey] longshot triggered')
    runLongshot().catch(e => log.error('[Hotkey] longshot failed:', e))
  })
}

// ---- 长截图 ----
ipcMain.handle(IPC_CHANNELS['longshot:selectWindow'], async () => {
  log.info('[IPC] longshot:selectWindow')
  const result = await LongshotService.listWindows()
  return result
})

// P2-2: 长截图抽为独立函数, 供 rail 点击 (IPC) 与全局热键复用
async function runLongshot(opts?: { window?: any }): Promise<{ success: boolean; filepath?: string; error?: string }> {
  log.info('[Longshot] start opts=', opts)
  // D7 修复: 原实现在此 hideMain()+sleep(500) 后才进入 start() 的倒计时,
  // 导致倒计时播放时侧边栏已隐藏, 用户看不到倒计时。
  // start() 内部在倒计时结束后自行 hideMain(), 无需在此提前隐藏。
  const filepath = await LongshotService.start(opts)
  WindowManager.showMain()
  if (filepath && Notification.isSupported()) {
    new Notification({ title: '长截图已保存', body: filepath, timeoutType: 'default' }).show()
  }
  return { success: !!filepath, filepath: filepath || undefined, error: filepath ? undefined : '长截图失败' }
}

ipcMain.handle(IPC_CHANNELS['longshot:start'], async (_event, opts?: { window?: any }) => {
  log.info('[IPC] longshot:start opts=', opts)
  return runLongshot(opts)
})

ipcMain.handle(IPC_CHANNELS['longshot:stop'], async () => {
  log.info('[IPC] longshot:stop')
  LongshotService.stop()
  return { success: true }
})

// ---- 截图: 批注 ----
// 透明屏幕绘图模式: 直接在真实屏幕上绘图, 不抓背景截图, 秒开秒画, 更直觉
// P2-2: 抽为独立函数, 供 rail 点击 (IPC) 与全局热键复用
async function runAnnotate(): Promise<{ success: boolean; filepath?: string; error?: string }> {
  log.info('[Annotate] (transparent overlay mode)')

  WindowManager.hideMain()
  await sleep(300)

  const target = DisplayService.sidebarTarget()
  const scaleFactor = target.scaleFactor

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

      // P1-8: 异步抓取屏幕截图, 供「含屏幕背景」导出模式使用
      // 不阻塞 init 发送, 截图就绪后通过独立通道推送
      CaptureService.grabTarget().then(grab => {
        if (!grab?.img || grab.img.isEmpty()) return
        const sf = grab.target.scaleFactor
        const imgSize = grab.img.getSize()
        const cropRect = {
          x: Math.max(0, Math.round((grab.workArea.x - grab.bounds.x) * sf)),
          y: Math.max(0, Math.round((grab.workArea.y - grab.bounds.y) * sf)),
          width: Math.min(Math.round(grab.workArea.width * sf), imgSize.width),
          height: Math.min(Math.round(grab.workArea.height * sf), imgSize.height)
        }
        if (cropRect.width < 2 || cropRect.height < 2) return

        let bgImg = grab.img.crop(cropRect)
        // Resize to exact canvas dimensions
        const cw = Math.round(target.workArea.width * scaleFactor)
        const ch = Math.round(target.workArea.height * scaleFactor)
        const bgSize = bgImg.getSize()
        if (bgSize.width !== cw || bgSize.height !== ch) {
          bgImg = bgImg.resize({ width: cw, height: ch })
        }
        const jpegBuf = bgImg.toJPEG(85)
        const dataUrl = 'data:image/jpeg;base64,' + jpegBuf.toString('base64')
        if (!activeAnnotatorWin || activeAnnotatorWin.isDestroyed()) return
        activeAnnotatorWin.webContents.send(IPC_CHANNELS['overlay:screenshot'], dataUrl)
        log.info(`[Annotator] Background screenshot sent (${cw}x${ch})`)
      }).catch((e: any) => {
        log.warn('[Annotator] Failed to grab background screenshot:', e?.message || e)
      })
    }
    // E9 修复: 先注册 overlay:ready 监听, 再创建窗口加载页面, 避免竞态丢信号
    ipcMain.on(IPC_CHANNELS['overlay:ready'], readyHandler)

    activeAnnotatorWin = WindowManager.showAnnotator()

    if (!activeAnnotatorWin) {
      ipcMain.removeListener(IPC_CHANNELS['overlay:ready'], readyHandler)
      WindowManager.showMain()
      resolve({ success: false, error: '无法创建批注窗口' })
      return
    }

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
}

ipcMain.handle(IPC_CHANNELS['capture:annotate'], async () => {
  log.info('[IPC] capture:annotate')
  return runAnnotate()
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

// E5 修复: USB 诊断专用通道, 不再复用 help:runDiagnostics (避免每次挂载 UsbPanel 触发全量诊断)
ipcMain.handle(IPC_CHANNELS['usb:getDiagnostics'], async () => {
  return UsbService.getDiagnostics()
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
// P0-6/B2: shell:openExternal 加协议白名单 (仅 http/https), 阻止 file:/javascript:/data: 等危险协议
ipcMain.handle(IPC_CHANNELS['shell:openExternal'], async (_event, url: string) => {
  if (typeof url !== 'string' || url.length > 2048) {
    log.warn('[SECURITY] shell:openExternal rejected: invalid url type or length')
    return
  }
  try {
    const parsed = new URL(url)
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
      log.warn(`[SECURITY] shell:openExternal rejected: protocol "${parsed.protocol}" not in whitelist`)
      return
    }
  } catch {
    log.warn(`[SECURITY] shell:openExternal rejected: malformed url "${url.slice(0, 100)}"`)
    return
  }
  await shell.openExternal(url)
})

// P0-6/B2: shell:openPath 加路径校验 — 必须是合法 Windows 绝对路径 (盘符开头), 阻止注入
ipcMain.handle(IPC_CHANNELS['shell:openPath'], async (_event, filePath: string) => {
  if (typeof filePath !== 'string' || filePath.length > 4096) {
    log.warn('[SECURITY] shell:openPath rejected: invalid path type or length')
    return false
  }
  // Windows 绝对路径: <盘符>:\... 或 \\...\... ; 阻止 javascript:/data: 等伪路径
  if (!/^[A-Za-z]:[\\/]|^\\\\/.test(filePath)) {
    log.warn(`[SECURITY] shell:openPath rejected: not an absolute Windows path "${filePath.slice(0, 100)}"`)
    return false
  }
  const result = await shell.openPath(filePath)
  return result === ''
})

// P0-6/B2: shell:showItemInFolder 同样校验路径格式
ipcMain.handle(IPC_CHANNELS['shell:showItemInFolder'], async (_event, filePath: string) => {
  if (typeof filePath !== 'string' || filePath.length > 4096) {
    log.warn('[SECURITY] shell:showItemInFolder rejected: invalid path type or length')
    return false
  }
  if (!/^[A-Za-z]:[\\/]|^\\\\/.test(filePath)) {
    log.warn(`[SECURITY] shell:showItemInFolder rejected: not an absolute Windows path "${filePath.slice(0, 100)}"`)
    return false
  }
  shell.showItemInFolder(filePath)
  return true
})

// ---- OOBE ----
ipcMain.handle(IPC_CHANNELS['oobe:getState'], async () => ConfigService.get().oobe)
ipcMain.handle(IPC_CHANNELS['oobe:setState'], async (_event, state) => {
  // E2 修复: OobeApp 传的是部分字段 (如 skip 只传 completed/skipped/lastStepIndex),
  // ConfigService.set 为整键覆盖, 直接写入会抹掉 role/prefs/completedAt 等字段,
  // 且下次启动 lastStepIndex 恢复逻辑失真 —— 改为与现有状态合并。
  const current = ConfigService.get().oobe
  return ConfigService.set('oobe', { ...current, ...state })
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

// ---- 快捷键槽位 (P2-2) ----
ipcMain.handle(IPC_CHANNELS['hotkey:getState'], async () => {
  return HotkeyService.getState()
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
// D12 修复: 保存通知引用以支持 dismiss
let activeNotification: Electron.Notification | null = null

ipcMain.handle(IPC_CHANNELS['notification:show'], async (_event, item: any) => {
  if (Notification.isSupported()) {
    // D12: 关闭上一条通知, 避免堆积
    if (activeNotification) {
      try { activeNotification.close() } catch { /* already closed */ }
    }
    const n = new Notification({
      title: item.title || '希沃侧边快捷键工具',
      body: item.message || '',
      timeoutType: item.duration > 0 ? 'default' : 'never'
    })
    n.on('click', () => {
      n.close()
    })
    n.on('close', () => {
      if (activeNotification === n) activeNotification = null
    })
    n.show()
    activeNotification = n
  }
  return true
})
ipcMain.handle(IPC_CHANNELS['notification:dismiss'], async () => {
  // D12 修复: 实际关闭当前通知
  if (activeNotification) {
    try { activeNotification.close() } catch { /* already closed */ }
    activeNotification = null
  }
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

// 辅助函数
function sleep(ms: number): Promise<void> {
  return new Promise(r => setTimeout(r, ms))
}

// ================= 启动 =================
app.whenReady().then(bootstrap)

// 退出清理
app.on('before-quit', () => {
  HotkeyService.unregisterAll()
  SchedulerService.stop()
  UsbService.stop()
  PrinterService.stop()
  ImeService.stop()
  RecorderService.cleanup()
  TrayService.destroy()
})

app.on('window-all-closed', () => {
  // Windows 下保持后台运行
})

// 崩溃恢复 (E8 修复: 区分窗口, 仅主窗口崩溃时重建)
app.on('render-process-gone', (_event, webContents, details) => {
  log.error(`[CRASH] Render process gone: ${details.reason}`)

  // 检查是否为 overlay/annotator 辅助窗口崩溃 — 这些窗口崩溃只需清理引用, 不重建主窗口
  if (activeOverlayWin && !activeOverlayWin.isDestroyed() && activeOverlayWin.webContents === webContents) {
    log.warn('[CRASH] Overlay window crashed, cleaning up')
    activeOverlayWin = null
    WindowManager.showMain()
    return
  }
  if (activeAnnotatorWin && !activeAnnotatorWin.isDestroyed() && activeAnnotatorWin.webContents === webContents) {
    log.warn('[CRASH] Annotator window crashed, cleaning up')
    activeAnnotatorWin = null
    WindowManager.showMain()
    return
  }

  // 主窗口崩溃 → 延迟重建
  setTimeout(() => {
    WindowManager.recreateMain(config!)
  }, 3000)
})

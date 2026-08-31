// windows/manager.ts - 窗口管理器

import { app, BrowserWindow } from 'electron'
import { join } from 'path'
import log from 'electron-log'
import type { SidekickConfig } from '../../shared/types'
import { DisplayService } from '../services/display'
import { ConfigService } from '../services/config'
import { AppBarService, getHwnd, EDGE_LEFT, EDGE_RIGHT } from '../services/appbar'

let sidebarWin: BrowserWindow | null = null
let oobeWin: BrowserWindow | null = null
let overlayWin: BrowserWindow | null = null
let annotatorWin: BrowserWindow | null = null
let settingsWin: BrowserWindow | null = null
let sidebarDocked = false
let sidebarRestoreBounds: { x: number; y: number; width: number; height: number } | null = null
let isAnimatingBounds = false

const isDev = !app.isPackaged

// ---- DPI/缩放策略 -------------------------------------------------
// Electron 窗口 bounds 一律为 DIP；Chromium 按系统 scaleFactor 自动把 DIP 映射为物理像素。
// 因此 4K@200% 的 DIP 视口与 1080p@100% 相同(1920x1080)，窗口保持固定 DIP 即可视觉一致；
// 真正需要处理的是「无缩放的大分辨率屏」(如 4K@100%: DIP=3840x2160)，此时固定 52px 会过小。
// 方案: uiScale = clamp(屏幕DIP宽度 / 1920, 1, 3)，窗口 DIP 尺寸 × uiScale，
// 配合 webContents.setZoomFactor(uiScale) 等比放大渲染内容，使 CSS 视口恒定、屏占比恒定。
const REFERENCE_WIDTH = 1920
const UI_SCALE_MIN = 1
const UI_SCALE_MAX = 3

function computeUiScale(workAreaWidth: number): number {
  const raw = workAreaWidth / REFERENCE_WIDTH
  return Math.min(UI_SCALE_MAX, Math.max(UI_SCALE_MIN, Math.round(raw * 100) / 100))
}

// ---- 窗口 bounds 平滑动画 (requestAnimationFrame 插值) ----
const ANIM_DURATION = 250 // ms
const ANIM_EASING = (t: number) => 1 - Math.pow(1 - t, 3) // easeOutCubic

function animateWindowBounds(
  win: BrowserWindow,
  target: { x: number; y: number; width: number; height: number }
): void {
  if (isAnimatingBounds) return
  isAnimatingBounds = true
  const start = win.getBounds()
  const startTime = Date.now()

  function tick() {
    if (win.isDestroyed()) {
      isAnimatingBounds = false
      return
    }
    const elapsed = Date.now() - startTime
    const progress = Math.min(elapsed / ANIM_DURATION, 1)
    const ease = ANIM_EASING(progress)

    const next = {
      x: Math.round(start.x + (target.x - start.x) * ease),
      y: Math.round(start.y + (target.y - start.y) * ease),
      width: Math.round(start.width + (target.width - start.width) * ease),
      height: Math.round(start.height + (target.height - start.height) * ease)
    }

    win.setBounds(next, true)

    if (progress < 1) {
      setImmediate(tick)
    } else {
      isAnimatingBounds = false
    }
  }

  tick()
}

function getPreloadPath(): string {
  return join(__dirname, '../preload/index.js')
}

function getRendererPath(view: string): string {
  if (isDev) {
    return `http://localhost:5173/#/${view}`
  }
  return join(__dirname, `../renderer/index.html`)
}

/** 辅助: 更新 AppBar 预留空间 (物理像素) */
function updateAppBarPos(win: BrowserWindow, dipWidth: number, dipHeight: number): void {
  if (!AppBarService.isAvailable()) return
  try {
    const target = DisplayService.sidebarTarget()
    const side = (ConfigService.get().display.sidebarSide) === 'right' ? 'right' : 'left'
    const edge = side === 'right' ? EDGE_RIGHT : EDGE_LEFT
    const hwnd = getHwnd(win)
    const physW = Math.round(dipWidth * target.scaleFactor)
    const physH = Math.round(dipHeight * target.scaleFactor)
    const physX = side === 'right'
      ? Math.round(target.bounds.x + target.bounds.width - physW)
      : Math.round(target.bounds.x)
    const physY = Math.round(target.bounds.y)
    AppBarService.queryAndSetPos(hwnd, edge, physX, physY, physW, physH)
  } catch (e: any) {
    log.warn('[AppBar] updateAppBarPos failed:', e.message)
  }
}



export const WindowManager = {
  createSidebar(config: SidekickConfig): BrowserWindow {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      return sidebarWin
    }

    const target = DisplayService.sidebarTarget()
    const uiScale = computeUiScale(target.workArea.width)
    const railWidth = Math.round(52 * uiScale)
    const sidebarHeight = target.workArea.height
    const side = config.display.sidebarSide === 'right' ? 'right' : 'left'
    const edge = side === 'right' ? EDGE_RIGHT : EDGE_LEFT

    // 计算初始 DIP 位置 (AppBar 未就绪时的 fallback)
    const dipX = side === 'right' ? target.workArea.x + target.workArea.width - railWidth : target.workArea.x
    const dipY = target.workArea.y

    sidebarWin = new BrowserWindow({
      x: dipX,
      y: dipY,
      width: railWidth,
      height: sidebarHeight,
      frame: false,
      transparent: false,
      resizable: false,
      movable: false,
      minimizable: false,
      maximizable: false,
      fullscreenable: false,
      skipTaskbar: true,
      alwaysOnTop: true, // fallback: AppBar 不可用时 (非 Windows / 模块加载失败) 仍保持置顶
      hasShadow: false,
      show: false,
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false,
        webSecurity: !isDev
      }
    })

    sidebarWin.webContents.setZoomFactor(uiScale)
    sidebarWin.setBackgroundColor('#ffffff')

    if (isDev) {
      sidebarWin.loadURL(getRendererPath('sidebar'))
      sidebarWin.webContents.openDevTools({ mode: 'detach' })
    } else {
      sidebarWin.loadFile(getRendererPath('sidebar'), { hash: '/sidebar' })
    }

    // 注册为 Windows AppBar: 系统从 WorkArea 预留侧边栏空间, 最大化窗口不会遮挡
    sidebarWin.on('ready-to-show', () => {
      log.info(`[Window] Sidebar ready (${railWidth} DIP x ${sidebarHeight} DIP, uiScale=${uiScale})`)

      if (AppBarService.isAvailable()) {
        try {
          const hwnd = getHwnd(sidebarWin!)
          AppBarService.register(hwnd)
          // 用物理像素告知系统预留空间, Electron 窗口随后用 DIP 定位
          const physW = Math.round(railWidth * target.scaleFactor)
          const physH = Math.round(sidebarHeight * target.scaleFactor)
          const physX = side === 'right'
            ? Math.round(target.bounds.x + target.bounds.width - physW)
            : Math.round(target.bounds.x)
          const physY = Math.round(target.bounds.y)
          AppBarService.queryAndSetPos(hwnd, edge, physX, physY, physW, physH)
          log.info(`[AppBar] Sidebar registered as AppBar (edge=${edge}, physW=${physW}, physH=${physH})`)
        } catch (e: any) {
          log.warn('[AppBar] Registration failed, using alwaysOnTop fallback:', e.message)
        }
      }
    })

    sidebarWin.on('closed', () => {
      sidebarWin = null
    })

    return sidebarWin
  },

  createOobe(): BrowserWindow {
    if (oobeWin && !oobeWin.isDestroyed()) {
      oobeWin.focus()
      return oobeWin
    }

    const primary = DisplayService.primary()
    const uiScale = computeUiScale(primary.workArea.width)
    const width = Math.round(960 * uiScale)
    const height = Math.round(640 * uiScale)
    const x = primary.workArea.x + Math.round((primary.workArea.width - width) / 2)
    const y = primary.workArea.y + Math.round((primary.workArea.height - height) / 2)

    // P0-1 修复: 原实现 parent=侧边栏(尚未 show) + modal: true —— Windows 上模态子窗
    // 挂在隐藏父窗时可见性不可靠(体检 A1), 模态在此也无意义(侧边栏本来就没显示)。
    // 改为独立置顶窗口; 「用户直接关闭 OOBE」的兜底见 main.ts bootstrap 的 closed 监听。
    oobeWin = new BrowserWindow({
      x,
      y,
      width,
      height,
      frame: false,
      resizable: false,
      minimizable: false,
      maximizable: false,
      alwaysOnTop: true,
      show: true,
      backgroundColor: '#f5f7fa',
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false
      }
    })
    oobeWin.webContents.setZoomFactor(uiScale)

    if (isDev) {
      oobeWin.loadURL(getRendererPath('oobe'))
    } else {
      oobeWin.loadFile(getRendererPath('oobe'), { hash: '/oobe' })
    }

    oobeWin.on('closed', () => {
      oobeWin = null
    })

    return oobeWin
  },

  showMain(): void {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      sidebarWin.show()
      sidebarWin.focus()
    }
  },

  closeOobe(): void {
    if (oobeWin && !oobeWin.isDestroyed()) {
      oobeWin.close()
      oobeWin = null
    }
  },

  focusMain(): void {
    if (oobeWin && !oobeWin.isDestroyed()) {
      oobeWin.focus()
      return
    }
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      sidebarWin.show()
      sidebarWin.focus()
    }
  },

  hideMain(): void {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      sidebarWin.hide()
    }
  },

  // 区域截图 overlay (透明,可看到桌面) — 覆盖侧边栏所在的「目标显示器」, 跨屏时跟随热插拔回退
  showOverlay(): BrowserWindow | null {
    // 防御: 若已有存活 overlay, 先关闭旧窗口, 避免窗口引用泄漏
    if (overlayWin && !overlayWin.isDestroyed()) {
      overlayWin.close()
    }
    const target = DisplayService.sidebarTarget()
    if (!target || !target.workArea || target.workArea.width <= 0 || target.workArea.height <= 0) {
      log.warn('[Window] showOverlay: invalid target display')
      return null
    }
    const win = new BrowserWindow({
      x: target.workArea.x,
      y: target.workArea.y,
      width: target.workArea.width,
      height: target.workArea.height,
      frame: false,
      transparent: true,
      resizable: false,
      movable: false,
      alwaysOnTop: true,
      skipTaskbar: true,
      fullscreenable: false,
      hasShadow: false,
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false
      }
    })

    if (isDev) {
      win.loadURL(getRendererPath('overlay'))
    } else {
      win.loadFile(getRendererPath('overlay'), { hash: '/overlay' })
    }

    // identity 守卫: 只有「本次创建」的窗口关闭时才清引用, 防止先关旧、后建新时误清新引用
    win.on('closed', () => {
      if (overlayWin === win) overlayWin = null
    })
    overlayWin = win

    return win
  },

  // 批注窗口 (透明覆盖层, 无需截图背景, 直接在真实屏幕上绘图)
  showAnnotator(): BrowserWindow | null {
    // 防御: 已有存活批注窗口时先关闭, 避免引用泄漏
    if (annotatorWin && !annotatorWin.isDestroyed()) {
      annotatorWin.close()
    }
    const target = DisplayService.sidebarTarget()
    if (!target || !target.workArea || target.workArea.width <= 0 || target.workArea.height <= 0) {
      log.warn('[Window] showAnnotator: invalid target display')
      return null
    }
    const win = new BrowserWindow({
      x: target.workArea.x,
      y: target.workArea.y,
      width: target.workArea.width,
      height: target.workArea.height,
      frame: false,
      transparent: true,
      resizable: false,
      movable: false,
      alwaysOnTop: true,
      skipTaskbar: true,
      fullscreenable: false,
      hasShadow: false,
      backgroundColor: '#00000000',
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false
      }
    })

    if (isDev) {
      win.loadURL(getRendererPath('annotate'))
    } else {
      win.loadFile(getRendererPath('annotate'), { hash: '/annotate' })
    }

    // 模块级引用 + identity 守卫: 窗口被外部关闭时自动置空, 不再依赖调用方手动管理
    win.on('closed', () => {
      if (annotatorWin === win) annotatorWin = null
    })
    annotatorWin = win

    return win
  },

  closeOverlay(): void {
    if (overlayWin && !overlayWin.isDestroyed()) {
      overlayWin.close()
      overlayWin = null
    }
  },

  closeAnnotator(): void {
    if (annotatorWin && !annotatorWin.isDestroyed()) {
      annotatorWin.close()
      annotatorWin = null
    }
  },

  showSettings(): BrowserWindow | null {
    if (settingsWin && !settingsWin.isDestroyed()) {
      settingsWin.focus()
      return settingsWin
    }

    const primary = DisplayService.primary()
    const uiScale = computeUiScale(primary.workArea.width)
    const width = Math.round(800 * uiScale)
    const height = Math.round(600 * uiScale)
    const x = primary.workArea.x + Math.round((primary.workArea.width - width) / 2)
    const y = primary.workArea.y + Math.round((primary.workArea.height - height) / 2)

    settingsWin = new BrowserWindow({
      x,
      y,
      width,
      height,
      frame: true,
      resizable: true,
      show: true,
      backgroundColor: '#f5f7fa',
      title: '希沃侧边快捷键工具 - 设置',
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true
      }
    })
    settingsWin.webContents.setZoomFactor(uiScale)

    if (isDev) {
      settingsWin.loadURL(getRendererPath('settings'))
    } else {
      settingsWin.loadFile(getRendererPath('settings'), { hash: '/settings' })
    }

    settingsWin.on('closed', () => {
      settingsWin = null
    })

    return settingsWin
  },

  broadcast(channel: string, data: unknown): void {
    for (const win of BrowserWindow.getAllWindows()) {
      if (win.webContents && !win.webContents.isDestroyed()) {
        win.webContents.send(channel, data)
      }
    }
  },

  recreateMain(config: SidekickConfig): void {
    // 崩溃重建时重置 docked 状态, 保证新窗口恢复为整列形态
    sidebarDocked = false
    sidebarRestoreBounds = null
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      sidebarWin.close()
    }
    setTimeout(() => {
      this.createSidebar(config)
      this.showMain()
    }, 500)
  },

  resizeMain(width: number, height: number): void {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      const cfg = DisplayService.sidebarTarget()
      const uiScale = computeUiScale(cfg.workArea.width)
      const side = (ConfigService.get().display.sidebarSide) === 'right' ? 'right' : 'left'
      // 渲染层传入的是基准 CSS 宽度(52/432), 换算为 DIP
      const railedWidth = Math.round(width * uiScale)

      let x: number
      if (side === 'right') {
        x = cfg.workArea.x + cfg.workArea.width - railedWidth
      } else {
        x = cfg.workArea.x
      }

      // P0-2 修复: 原实现 (height || workArea.height) * uiScale —— 渲染层固定传 height=0
      // (SidebarApp.vue watch isExpanded), 0 为 falsy 落到 workArea.height, 而该值已是 DIP,
      // 再乘 uiScale 即双乘: 4K@100% (workArea.height=2160, uiScale=2) → 4320 DIP 飞出屏幕。
      // 正确语义: 0 = 铺满 workArea (DIP, 与 createSidebar 一致, 不乘);
      // >0 = 渲染层 CSS 基准高度 (与 width 同语义, 需乘 uiScale 转 DIP)。
      sidebarWin.setBounds({
        x,
        y: cfg.workArea.y,
        width: railedWidth,
        height: height > 0 ? Math.round(height * uiScale) : cfg.workArea.height
      }, true)
      // 更新 AppBar 预留空间
      updateAppBarPos(sidebarWin, railedWidth, height > 0 ? Math.round(height * uiScale) : cfg.workArea.height)
    }
  },

  /** 收起侧边栏到底部保留小条 */
  dockMain(): void {
    if (!sidebarWin || sidebarWin.isDestroyed()) return
    if (sidebarDocked) return
    sidebarDocked = true
    const bounds = sidebarWin.getBounds()
    sidebarRestoreBounds = { x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height }

    const cfg = DisplayService.sidebarTarget()
    const side = (ConfigService.get().display.sidebarSide) === 'right' ? 'right' : 'left'
    const uiScale = computeUiScale(cfg.workArea.width)
    const railWidth = Math.round(52 * uiScale)
    const dockHeight = Math.round(52 * uiScale)

    let x: number
    if (side === 'right') {
      x = cfg.workArea.x + cfg.workArea.width - railWidth
    } else {
      x = cfg.workArea.x
    }

    const dockY = cfg.workArea.y + cfg.workArea.height - dockHeight
    animateWindowBounds(sidebarWin, {
      x,
      y: dockY,
      width: railWidth,
      height: dockHeight
    })
    log.info(`[Window] Sidebar docked (${railWidth}x${dockHeight} DIP, uiScale=${uiScale})`)
  },

  /** 恢复侧边栏 */
  undockMain(): void {
    if (!sidebarWin || sidebarWin.isDestroyed()) return
    if (!sidebarDocked) return
    sidebarDocked = false
    const cfg = DisplayService.sidebarTarget()
    const uiScale = computeUiScale(cfg.workArea.width)
    const railWidth = Math.round(52 * uiScale)

    if (sidebarRestoreBounds) {
      // 恢复之前保存的尺寸 (已是 DIP)
      const side = (ConfigService.get().display.sidebarSide) === 'right' ? 'right' : 'left'
      const restoredWidth = sidebarRestoreBounds.width > Math.round(100 * uiScale) ? sidebarRestoreBounds.width : railWidth
      let x: number
      if (side === 'right') {
        x = cfg.workArea.x + cfg.workArea.width - restoredWidth
      } else {
        x = cfg.workArea.x
      }
      animateWindowBounds(sidebarWin, {
        x,
        y: cfg.workArea.y,
        width: restoredWidth,
        height: cfg.workArea.height
      })
    }
    sidebarRestoreBounds = null
    log.info('[Window] Sidebar undocked')
  },

  /** DPI/显示器变更时重建侧边栏位置 (带边界校验, 防拔屏后飞出屏幕外) */
  onDisplayChanged(): void {
    if (!sidebarWin || sidebarWin.isDestroyed()) return
    const cfg = DisplayService.sidebarTarget()
    // 边界校验: 目标显示器 workArea 必须有效; 显示列表尚未更新/失效时跳过本次重定位
    if (!cfg || !cfg.workArea || cfg.workArea.width <= 0 || cfg.workArea.height <= 0) {
      log.warn('[Window] onDisplayChanged: invalid target display, skip reposition')
      return
    }
    const uiScale = computeUiScale(cfg.workArea.width)
    const railWidth = Math.round(52 * uiScale)
    const side = (ConfigService.get().display.sidebarSide) === 'right' ? 'right' : 'left'

    let x: number
    if (side === 'right') {
      x = cfg.workArea.x + cfg.workArea.width - railWidth
    } else {
      x = cfg.workArea.x
    }

    sidebarWin.webContents.setZoomFactor(uiScale)

    // 收起条状态下只重算底部条位置, 不弹回整列
    if (sidebarDocked) {
      const dockHeight = Math.round(52 * uiScale)
      const dockY = cfg.workArea.y + cfg.workArea.height - dockHeight
      sidebarWin.setBounds({
        x,
        y: dockY,
        width: railWidth,
        height: dockHeight
      }, true)
      updateAppBarPos(sidebarWin, railWidth, dockHeight)
      log.info(`[Window] Docked sidebar repositioned: ${railWidth}x${dockHeight} @ (${x}, ${dockY}) uiScale=${uiScale}`)
      return
    }

    sidebarWin.setBounds({
      x,
      y: cfg.workArea.y,
      width: railWidth,
      height: cfg.workArea.height
    }, true)
    updateAppBarPos(sidebarWin, railWidth, cfg.workArea.height)
    log.info(`[Window] Sidebar repositioned: ${railWidth}x${cfg.workArea.height} @ (${x}, ${cfg.workArea.y}) uiScale=${uiScale}`)
  },

  isDocked(): boolean {
    return sidebarDocked
  },

  /** 应用退出前调用: 注销 AppBar (系统恢复原始 WorkArea) */
  destroy(): void {
    if (sidebarWin && !sidebarWin.isDestroyed() && AppBarService.isRegistered()) {
      try {
        AppBarService.remove(getHwnd(sidebarWin))
      } catch { /* ignore */ }
    }
  }
}

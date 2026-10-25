// windows/manager.ts - 窗口管理器

import { app, BrowserWindow, screen } from 'electron'
import { join } from 'path'
import log from 'electron-log'
import type { SidekickConfig } from '../../shared/types'
import { DisplayService } from '../services/display'
import { ConfigService } from '../services/config'
import { AppBarService, getHwnd, EDGE_LEFT, EDGE_RIGHT } from '../services/appbar'
import { AppearanceService, initialBackgroundColor } from '../services/appearance'
import {
  computeFanLayout,
  collapsedLayout,
  resolveBallPosition,
  clampToArea,
  snapToEdges,
  toRelativePosition
} from '../../shared/floatball-layout'

// 侧边栏轨道基准宽度 (CSS px, 与渲染层 RAIL_WIDTH 对齐; v1.1 液态玻璃为 72)
const RAIL_BASE = 72
// v1.1 悬浮式玻璃胶囊: 上下各留 8% workArea 边距, docked 时为 72x72 圆钮
const SIDEBAR_MARGIN_RATIO = 0.08

let sidebarWin: BrowserWindow | null = null
let oobeWin: BrowserWindow | null = null
let overlayWin: BrowserWindow | null = null
let annotatorWin: BrowserWindow | null = null
let settingsWin: BrowserWindow | null = null
let floatBallWin: BrowserWindow | null = null
let sidebarDocked = false
let sidebarRestoreBounds: { x: number; y: number; width: number; height: number } | null = null
let isAnimatingBounds = false
let floatBallPos: { x: number; y: number } | null = null
let floatBallExpanded = false
let floatBallDragTimer: NodeJS.Timeout | null = null
let floatBallGrab = { x: 0, y: 0 }
let floatBallCollapseTimer: NodeJS.Timeout | null = null
let floatBallHiddenForCapture = false

function stopFloatBallDrag(): void {
  if (floatBallDragTimer) {
    clearInterval(floatBallDragTimer)
    floatBallDragTimer = null
  }
}

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
    const railWidth = Math.round(RAIL_BASE * uiScale)
    // v1.1 悬浮式玻璃胶囊: 上下各留 8% 边距 (与渲染层 .rail 圆角胶囊对齐)
    const sidebarHeight = Math.round(target.workArea.height * (1 - SIDEBAR_MARGIN_RATIO * 2))
    const sidebarY = target.workArea.y + Math.round(target.workArea.height * SIDEBAR_MARGIN_RATIO)
    const side = config.display.sidebarSide === 'right' ? 'right' : 'left'
    const edge = side === 'right' ? EDGE_RIGHT : EDGE_LEFT

    // 计算初始 DIP 位置 (AppBar 未就绪时的 fallback)
    const dipX = side === 'right' ? target.workArea.x + target.workArea.width - railWidth : target.workArea.x
    const dipY = sidebarY

    sidebarWin = new BrowserWindow({
      x: dipX,
      y: dipY,
      width: railWidth,
      height: sidebarHeight,
      frame: false,
      transparent: true,
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
    sidebarWin.setBackgroundColor(initialBackgroundColor('sidebar'))

    if (isDev) {
      sidebarWin.loadURL(getRendererPath('sidebar'))
      sidebarWin.webContents.openDevTools({ mode: 'detach' })
    } else {
      sidebarWin.loadFile(getRendererPath('sidebar'), { hash: '/sidebar' })
    }

    // 注册为 Windows AppBar: 系统从 WorkArea 预留侧边栏空间, 最大化窗口不会遮挡
    sidebarWin.on('ready-to-show', () => {
      log.info(`[Window] Sidebar ready (${railWidth} DIP x ${sidebarHeight} DIP, uiScale=${uiScale})`)
      AppearanceService.register(sidebarWin!, 'sidebar')

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

  /** 切换侧边栏显隐 (悬浮球「侧边栏」动作用) */
  toggleMain(): void {
    if (sidebarWin && !sidebarWin.isDestroyed()) {
      if (sidebarWin.isVisible()) sidebarWin.hide()
      else {
        sidebarWin.show()
        sidebarWin.focus()
      }
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

  // ==================== 悬浮球 ====================
  //
  // 关键决策 (从 v1.1 编译产物重建):
  //  1. focusable:false —— 悬浮球不能抢前台, 否则「切换输入法」会把前台切到球自己。
  //  2. 拖拽走主进程轮询光标, 不用 -webkit-app-region: drag (透明+focusable:false 时灵时不灵, 且无法贴边吸附)。
  //  3. 球位置单独记在 floatBallPos, 不从窗口 bounds 反推 (展开时窗口放大到扇形包围盒)。
  //  4. 只在「侧边栏所在那块屏」活动, 拖拽 clamp 有唯一事实源。
  createFloatBall(config: SidekickConfig): BrowserWindow | null {
    if (floatBallWin && !floatBallWin.isDestroyed()) return floatBallWin
    if (!config.floatBall.enabled) return null
    const target = DisplayService.sidebarTarget()
    if (!target || !target.workArea || target.workArea.width <= 0 || target.workArea.height <= 0) {
      log.warn('[Window] createFloatBall: invalid target display')
      return null
    }
    const uiScale = computeUiScale(target.workArea.width)
    const ballDip = Math.round(config.floatBall.size * uiScale)
    const pos = resolveBallPosition(config.floatBall, target.workArea, ballDip, config.display.sidebarSide)
    floatBallPos = pos
    floatBallExpanded = false
    floatBallWin = new BrowserWindow({
      x: pos.x,
      y: pos.y,
      width: ballDip,
      height: ballDip,
      frame: false,
      transparent: true,
      resizable: false,
      movable: false,
      minimizable: false,
      maximizable: false,
      fullscreenable: false,
      skipTaskbar: true,
      alwaysOnTop: true,
      hasShadow: false,
      focusable: false,
      acceptFirstMouse: true,
      backgroundColor: '#00000000',
      show: false,
      webPreferences: {
        preload: getPreloadPath(),
        nodeIntegration: false,
        contextIsolation: true,
        sandbox: false,
        webSecurity: !isDev,
        // 透明窗口默认背景节流会让扇形展开动画掉帧
        backgroundThrottling: false
      }
    })
    floatBallWin.setAlwaysOnTop(true, 'screen-saver')
    floatBallWin.webContents.setZoomFactor(uiScale)
    if (isDev) {
      floatBallWin.loadURL(getRendererPath('floatball'))
    } else {
      floatBallWin.loadFile(getRendererPath('floatball'), { hash: '/floatball' })
    }
    floatBallWin.on('ready-to-show', () => {
      floatBallWin?.showInactive()
      if (floatBallWin) AppearanceService.register(floatBallWin, 'floatball')
      floatBallWin?.webContents.send('floatball:layout', collapsedLayout(config.floatBall.size))
      log.info(`[Window] FloatBall ready (${ballDip} DIP @ ${pos.x},${pos.y}, uiScale=${uiScale})`)
    })
    floatBallWin.on('closed', () => {
      stopFloatBallDrag()
      if (floatBallCollapseTimer) {
        clearTimeout(floatBallCollapseTimer)
        floatBallCollapseTimer = null
      }
      floatBallWin = null
      floatBallExpanded = false
    })
    return floatBallWin
  },
  getFloatBall(): BrowserWindow | null {
    return floatBallWin && !floatBallWin.isDestroyed() ? floatBallWin : null
  },
  showFloatBall(): void {
    const cfg = ConfigService.get()
    if (!cfg.floatBall.enabled) return
    if (!floatBallWin || floatBallWin.isDestroyed()) {
      this.createFloatBall(cfg)
      return
    }
    floatBallWin.showInactive()
    floatBallWin.setAlwaysOnTop(true, 'screen-saver')
  },
  hideFloatBall(): void {
    if (!floatBallWin || floatBallWin.isDestroyed()) return
    this.collapseFloatBall(true)
    floatBallWin.hide()
  },
  toggleFloatBall(): void {
    if (floatBallWin && !floatBallWin.isDestroyed() && floatBallWin.isVisible()) {
      this.hideFloatBall()
    } else {
      this.showFloatBall()
    }
  },
  destroyFloatBall(): void {
    stopFloatBallDrag()
    if (floatBallWin && !floatBallWin.isDestroyed()) floatBallWin.close()
    floatBallWin = null
    floatBallPos = null
    floatBallExpanded = false
  },
  /** 截图 / 批注前临时隐藏, 免得球被拍进图里 */
  hideFloatBallForCapture(): void {
    if (!floatBallWin || floatBallWin.isDestroyed() || !floatBallWin.isVisible()) return
    floatBallHiddenForCapture = true
    this.collapseFloatBall(true)
    floatBallWin.hide()
  },
  /** 截图 / 批注结束后恢复 (只恢复「是我们藏起来的」那种情况) */
  restoreFloatBallAfterCapture(): void {
    if (!floatBallHiddenForCapture) return
    floatBallHiddenForCapture = false
    if (!ConfigService.get().floatBall.enabled) return
    if (floatBallWin && !floatBallWin.isDestroyed()) {
      floatBallWin.showInactive()
      floatBallWin.setAlwaysOnTop(true, 'screen-saver')
    }
  },
  /** 开始拖拽: grab 为指针相对球左上角的 CSS px 偏移 */
  floatBallDragStart(grab?: { x: number; y: number }): void {
    if (!floatBallWin || floatBallWin.isDestroyed()) return
    const target = DisplayService.sidebarTarget()
    const uiScale = computeUiScale(target.workArea.width)
    const ballDip = Math.round(ConfigService.get().floatBall.size * uiScale)
    this.collapseFloatBall(true)
    floatBallGrab = {
      x: Math.round((grab?.x ?? ballDip / 2 / uiScale) * uiScale),
      y: Math.round((grab?.y ?? ballDip / 2 / uiScale) * uiScale)
    }
    stopFloatBallDrag()
    floatBallDragTimer = setInterval(() => {
      if (!floatBallWin || floatBallWin.isDestroyed()) {
        stopFloatBallDrag()
        return
      }
      const cur = screen.getCursorScreenPoint()
      const area = DisplayService.sidebarTarget().workArea
      const next = clampToArea(
        { x: cur.x - floatBallGrab.x, y: cur.y - floatBallGrab.y },
        { width: ballDip, height: ballDip },
        area
      )
      floatBallPos = next
      floatBallWin.setBounds({ x: next.x, y: next.y, width: ballDip, height: ballDip }, false)
    }, 16)
  },
  /** 松手: 贴边吸附 + 位置持久化 */
  floatBallDragEnd(): void {
    stopFloatBallDrag()
    if (!floatBallWin || floatBallWin.isDestroyed() || !floatBallPos) return
    const cfg = ConfigService.get()
    const target = DisplayService.sidebarTarget()
    const uiScale = computeUiScale(target.workArea.width)
    const ballDip = Math.round(cfg.floatBall.size * uiScale)
    const snapped = snapToEdges(
      floatBallPos,
      { width: ballDip, height: ballDip },
      target.workArea,
      Math.round(cfg.floatBall.snapThreshold * uiScale)
    )
    floatBallPos = { x: snapped.x, y: snapped.y }
    floatBallWin.setBounds({ x: snapped.x, y: snapped.y, width: ballDip, height: ballDip }, false)
    const rel = toRelativePosition(floatBallPos, target.workArea)
    ConfigService.set('floatBall.x', rel.x)
    ConfigService.set('floatBall.y', rel.y)
    log.info(`[Window] FloatBall dropped @ ${snapped.x},${snapped.y} edge=${snapped.edge ?? 'none'}`)
  },
  /** 展开扇形菜单: 先放大窗口腾出空间, 再下发布局让渲染层做飞出动画 */
  expandFloatBall(): void {
    if (!floatBallWin || floatBallWin.isDestroyed()) return
    if (floatBallCollapseTimer) {
      clearTimeout(floatBallCollapseTimer)
      floatBallCollapseTimer = null
    }
    const cfg = ConfigService.get()
    const target = DisplayService.sidebarTarget()
    const uiScale = computeUiScale(target.workArea.width)
    const count = cfg.floatBall.actions.length
    if (count === 0) return
    if (!floatBallPos) {
      const b = floatBallWin.getBounds()
      floatBallPos = { x: b.x, y: b.y }
    }
    const { layout, window: winBounds } = computeFanLayout({
      ball: floatBallPos,
      ballSize: cfg.floatBall.size,
      area: target.workArea,
      count,
      uiScale
    })
    floatBallExpanded = true
    floatBallWin.setBounds(winBounds, false)
    floatBallWin.webContents.send('floatball:layout', layout)
  },
  /** 收起扇形菜单. immediate=true 时立刻缩窗口 (拖拽/隐藏用), 否则等收起动画播完 */
  collapseFloatBall(immediate = false): void {
    if (!floatBallWin || floatBallWin.isDestroyed()) return
    if (!floatBallExpanded) return
    floatBallExpanded = false
    const cfg = ConfigService.get()
    const target = DisplayService.sidebarTarget()
    const uiScale = computeUiScale(target.workArea.width)
    const ballDip = Math.round(cfg.floatBall.size * uiScale)
    floatBallWin.webContents.send('floatball:layout', collapsedLayout(cfg.floatBall.size))
    const shrink = () => {
      floatBallCollapseTimer = null
      if (!floatBallWin || floatBallWin.isDestroyed()) return
      if (floatBallExpanded) return
      const pos = clampToArea(
        floatBallPos ?? { x: floatBallWin.getBounds().x, y: floatBallWin.getBounds().y },
        { width: ballDip, height: ballDip },
        target.workArea
      )
      floatBallPos = pos
      floatBallWin.setBounds({ x: pos.x, y: pos.y, width: ballDip, height: ballDip }, false)
    }
    if (floatBallCollapseTimer) {
      clearTimeout(floatBallCollapseTimer)
      floatBallCollapseTimer = null
    }
    if (immediate) {
      shrink()
    } else {
      floatBallCollapseTimer = setTimeout(shrink, 190)
    }
  },
  isFloatBallExpanded(): boolean {
    return floatBallExpanded
  },
  /** 悬浮球配置变更后重新套用 (开关 / 尺寸 / 位置) */
  applyFloatBallConfig(config: SidekickConfig): void {
    if (!config.floatBall.enabled) {
      this.destroyFloatBall()
      return
    }
    if (!floatBallWin || floatBallWin.isDestroyed()) {
      this.createFloatBall(config)
      return
    }
    const target = DisplayService.sidebarTarget()
    const uiScale = computeUiScale(target.workArea.width)
    const ballDip = Math.round(config.floatBall.size * uiScale)
    this.collapseFloatBall(true)
    const pos = resolveBallPosition(config.floatBall, target.workArea, ballDip, config.display.sidebarSide)
    floatBallPos = pos
    floatBallWin.webContents.setZoomFactor(uiScale)
    floatBallWin.setBounds({ x: pos.x, y: pos.y, width: ballDip, height: ballDip }, false)
    floatBallWin.webContents.send('floatball:layout', collapsedLayout(config.floatBall.size))
    if (!floatBallWin.isVisible()) floatBallWin.showInactive()
  },
  /** 显示器变更时把球拉回有效工作区 */
  onFloatBallDisplayChanged(): void {
    if (!floatBallWin || floatBallWin.isDestroyed()) return
    const cfg = ConfigService.get()
    const target = DisplayService.sidebarTarget()
    if (!target || !target.workArea || target.workArea.width <= 0) {
      log.warn('[Window] onFloatBallDisplayChanged: invalid target display, skip')
      return
    }
    this.applyFloatBallConfig(cfg)
    log.info('[Window] FloatBall repositioned after display change')
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
        y: cfg.workArea.y + Math.round(cfg.workArea.height * SIDEBAR_MARGIN_RATIO),
        width: railedWidth,
        height: height > 0 ? Math.round(height * uiScale) : Math.round(cfg.workArea.height * (1 - SIDEBAR_MARGIN_RATIO * 2))
      }, true)
      const bh = height > 0 ? Math.round(height * uiScale) : Math.round(cfg.workArea.height * (1 - SIDEBAR_MARGIN_RATIO * 2))
      // 更新 AppBar 预留空间
      updateAppBarPos(sidebarWin, railedWidth, bh)
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
    const railWidth = Math.round(RAIL_BASE * uiScale)
    const dockHeight = Math.round(RAIL_BASE * uiScale)

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
    const railWidth = Math.round(RAIL_BASE * uiScale)

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
        y: cfg.workArea.y + Math.round(cfg.workArea.height * SIDEBAR_MARGIN_RATIO),
        width: restoredWidth,
        height: Math.round(cfg.workArea.height * (1 - SIDEBAR_MARGIN_RATIO * 2))
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
    const railWidth = Math.round(RAIL_BASE * uiScale)
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
      const dockHeight = Math.round(RAIL_BASE * uiScale)
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

    const bodyHeight = Math.round(cfg.workArea.height * (1 - SIDEBAR_MARGIN_RATIO * 2))
    const bodyY = cfg.workArea.y + Math.round(cfg.workArea.height * SIDEBAR_MARGIN_RATIO)
    sidebarWin.setBounds({
      x,
      y: bodyY,
      width: railWidth,
      height: bodyHeight
    }, true)
    updateAppBarPos(sidebarWin, railWidth, bodyHeight)
    log.info(`[Window] Sidebar repositioned: ${railWidth}x${bodyHeight} @ (${x}, ${bodyY}) uiScale=${uiScale}`)
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

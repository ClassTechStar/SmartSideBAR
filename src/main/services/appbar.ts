// src/main/services/appbar.ts - Windows AppBar API 封装
//
// 使用 SHAppBarMessage 向系统注册"应用栏", 系统会自动将 WorkArea 缩小以预留侧边栏空间,
// 其他最大化窗口就不会遮挡侧边栏。https://learn.microsoft.com/zh-cn/windows/win32/shell/application-desktop-toolbars
//
// 原生模块加载: electron-vite 构建时将 build/Release/appbar.node 复制到 out/main/,
// electron-builder 打包时通过 asarUnpack 将 .node 文件解压到 app.asar.unpack/。

import { join } from 'path'
import { app, BrowserWindow } from 'electron'
import log from 'electron-log'

/** 从 Electron BrowserWindow 提取原生 Win32 HWND (4字节整数) */
export function getHwnd(win: BrowserWindow): number {
  const buf = win.getNativeWindowHandle()
  return process.arch === 'ia32' ? buf.readInt32LE(0) : buf.readBigUInt64LE(0) as unknown as number
}

// ---- 原生模块加载 ----
// electron-vite 构建时将 build/Release/appbar.node 复制到 out/main/,
// electron-builder 打包时通过 asarUnpack 将 .node 文件解压到 app.asar.unpack/。
// 开发模式直接从 build/Release 加载。
let _native: any = null
function getNative(): any {
  if (_native) return _native
  // 打包后: app.asar.unpack/out/main/appbar.node (asarUnpack 解压)
  if (app.isPackaged) {
    try {
      const asarDir = app.getAppPath() // .../app.asar
      const unpackedPath = join(asarDir.replace('app.asar', 'app.asar.unpack'), 'out', 'main', 'appbar.node')
      _native = require(unpackedPath)
      log.info('[AppBar] Loaded from unpacked asar:', unpackedPath)
      return _native
    } catch { /* fallback */ }
  }
  // 开发模式: build/Release/appbar.node (node-gyp 构建产物)
  try {
    const projectRoot = join(__dirname, '..', '..', '..')
    const devPath = join(projectRoot, 'build', 'Release', 'appbar.node')
    _native = require(devPath)
    log.info('[AppBar] Loaded from build/Release:', devPath)
    return _native
  } catch (e: any) {
    log.warn('[AppBar] Native module not available:', e.message)
    return null
  }
}

export const EDGE_LEFT = 0
export const EDGE_TOP = 1
export const EDGE_RIGHT = 2
export const EDGE_BOTTOM = 3

export type AppBarEdge = typeof EDGE_LEFT | typeof EDGE_TOP | typeof EDGE_RIGHT | typeof EDGE_BOTTOM

interface AppBarRect { x: number; y: number; w: number; h: number }
interface TaskbarInfo { edge: AppBarEdge; left: number; top: number; right: number; bottom: number }

let registered = false

export const AppBarService = {
  /** 检查原生模块是否可用 (非 Windows 平台或编译失败时返回 false) */
  isAvailable(): boolean {
    return !!getNative()
  },

  /**
   * 注册应用栏。必须在侧边栏窗口 ready-to-show 后调用。
   * 注册后系统会从 WorkArea 中扣除应用栏占用的空间。
   */
  register(hwnd: number): boolean {
    const native = getNative()
    if (!native) return false

    // 使用 WM_USER +1 作为回调消息 ID (系统会通过此消息通知应用栏状态变化)
    const callbackMsg = 0x0400 + 1 // WM_USER + 1
    try {
      const ok = native.register(hwnd, callbackMsg)
      if (ok) {
        registered = true
        log.info(`[AppBar] Registered (hwnd=${hwnd})`)
      }
      return ok
    } catch (e: any) {
      log.error('[AppBar] register failed:', e.message)
      return false
    }
  },

  /**
   * 调用 ABM_QUERYPOS + ABM_SETPOS 预留屏幕空间 (物理像素)。
   * 不调用 MoveWindow — Electron 窗口由调用方通过 setBounds(DIP) 单独定位。
   */
  queryAndSetPos(hwnd: number, edge: AppBarEdge, x: number, y: number, w: number, h: number): AppBarRect | null {
    const native = getNative()
    if (!native) return null
    try {
      const rect = native.queryAndSetPos(hwnd, edge, x, y, w, h)
      log.debug(`[AppBar] queryAndSetPos edge=${edge} -> (${rect.x},${rect.y}) ${rect.w}x${rect.h}`)
      return rect
    } catch (e: any) {
      log.error('[AppBar] setPos failed:', e.message)
      return null
    }
  },

  /** 注销应用栏。退出前必须调用。 */
  remove(hwnd: number): void {
    const native = getNative()
    if (!native || !registered) return
    try {
      native.remove(hwnd)
      registered = false
      log.info(`[AppBar] Removed (hwnd=${hwnd})`)
    } catch (e: any) {
      log.error('[AppBar] remove failed:', e.message)
    }
  },

  /** 获取任务栏位置信息 */
  getTaskbarPos(): TaskbarInfo | null {
    const native = getNative()
    if (!native) return null
    try { return native.getTaskbarPos() }
    catch { return null }
  },

  /** 是否已注册 */
  isRegistered(): boolean { return registered }
}

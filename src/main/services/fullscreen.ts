// services/fullscreen.ts - 全屏应用监听 (v1.3 新增)
//
// 背景: 1.2 的侧边栏依赖 AppBar 注册 + alwaysOnTop 遮挡, 全屏授课应用
// (希沃白板/视频播放器) 无法让侧边栏自动收缩。本服务轮询前台窗口,
// 当其完整覆盖侧边栏所在显示器时视为「全屏应用在场」:
//   - 进入全屏 -> 自动隐藏侧边栏
//   - 退出全屏 -> 自动恢复侧边栏 (若此前因全屏被隐藏)
//
// 实现说明:
//   - koffi 为可选依赖 (与 appearance.ts 同策略), 加载失败时监听静默停用。
//   - 排除本进程 own 窗口 (截图 overlay / 批注层 / OOBE / 设置均为本进程),
//     避免截图框选层触发误隐藏。
//   - 仅轮询, 无窗口钩子, 不影响 1.2 任何既有样式与交互逻辑。

import { screen } from 'electron'
import log from 'electron-log'

let koffi: any = null
try {
  koffi = require('koffi')
} catch {
  koffi = null
}

let api: any = null
let apiFailed = false
function ensure(): any {
  if (api) return api
  if (apiFailed || !koffi) return null
  try {
    const user32 = koffi.load('user32.dll')
    const RECT = koffi.struct('FS_RECT', {
      left: 'long',
      top: 'long',
      right: 'long',
      bottom: 'long'
    })
    api = {
      GetForegroundWindow: user32.func('GetForegroundWindow', 'uintptr_t', []),
      GetWindowRect: user32.func('GetWindowRect', 'bool', ['uintptr_t', koffi.pointer(RECT)]),
      GetWindowThreadProcessId: user32.func('GetWindowThreadProcessId', 'uint32', [
        'uintptr_t',
        koffi.pointer('uint32')
      ])
    }
    return api
  } catch (e) {
    apiFailed = true
    log.warn('[Fullscreen] FFI 装配失败, 全屏监听停用:', e)
    return null
  }
}

export interface FullscreenWatcherCallbacks {
  /** 侧边栏所在显示器物理矩形 */
  getMonitorRect: () => { x: number; y: number; width: number; height: number } | null
  /** 进入全屏: 返回 true 表示本次全屏期间侧边栏由监听器负责隐藏 */
  onEnter: () => boolean
  /** 退出全屏: owned=true 表示此前由监听器隐藏, 应恢复 */
  onLeave: (owned: boolean) => void
}

let timer: NodeJS.Timeout | null = null
let active = false // 前台全屏应用在场
let owned = false // 侧边栏是否由本监听器隐藏

/** 判定前台窗口是否覆盖整块显示器 (全屏授课/视频/放映) */
function isForegroundFullscreen(a: any, monitor: { x: number; y: number; width: number; height: number }): boolean {
  try {
    const hwnd = a.GetForegroundWindow()
    if (!hwnd) return false
    const pidBuf = koffi.alloc('uint32', 1)
    a.GetWindowThreadProcessId(hwnd, pidBuf)
    const pid = Number(pidBuf[0])
    if (pid === process.pid) return false // 本进程窗口 (overlay/批注/OOBE) 不算全屏
    const rect = { left: 0, top: 0, right: 0, bottom: 0 }
    if (!a.GetWindowRect(hwnd, rect)) return false
    const tol = 2 // 物理像素容差
    return (
      rect.left <= monitor.x + tol &&
      rect.top <= monitor.y + tol &&
      rect.right >= monitor.x + monitor.width - tol &&
      rect.bottom >= monitor.y + monitor.height - tol
    )
  } catch {
    return false
  }
}

export const FullscreenService = {
  isAvailable(): boolean {
    return ensure() !== null
  },

  start(cb: FullscreenWatcherCallbacks): void {
    if (timer) return
    const a = ensure()
    if (!a) return
    timer = setInterval(() => {
      const monitor = cb.getMonitorRect()
      if (!monitor) return
      const fs = isForegroundFullscreen(a, monitor)
      if (fs && !active) {
        active = true
        owned = cb.onEnter()
        if (owned) log.info('[Fullscreen] 全屏应用在场, 侧边栏已自动收缩')
      } else if (!fs && active) {
        active = false
        cb.onLeave(owned)
        if (owned) log.info('[Fullscreen] 全屏结束, 侧边栏已恢复')
        owned = false
      }
    }, 400)
    log.info('[Fullscreen] 全屏监听已启动 (400ms 轮询)')
  },

  stop(): void {
    if (timer) {
      clearInterval(timer)
      timer = null
    }
  }
}

// services/appearance.ts - 液态玻璃外观服务 (从 v1.1 编译产物重建)
//
// 职责:
//  1. 维护当前外观配置 (clampAppearance) + 主题 (light/dark/auto)
//  2. 通过 koffi FFI 给「非透明」窗口 (设置/OOBE) 挂 Windows 原生亚克力/云母背板;
//     sidebar / floatball 走纯 CSS 玻璃, 这里显式清掉系统背板避免"方形底板"。
//  3. 主题/配置变更时向所有窗口广播 appearance:changed, 渲染层重注入 --lg-* 变量。
//
// koffi 为可选依赖: 加载失败时 native 材质退化为 "none", 玻璃观感由 CSS 承载, 功能不受影响。

import { app, BrowserWindow, nativeTheme, screen } from 'electron'
import * as os from 'os'
import log from 'electron-log'
import type { AppearanceConfig, AppearanceSnapshot } from '../../shared/types'
import {
  DEFAULT_APPEARANCE,
  clampAppearance,
  resolveTheme,
  resolveMaterial,
  supportsNativeMaterial,
  parseWindowsBuild,
  accentGradientAbgr
} from '../../shared/appearance'
import { ConfigService } from './config'
import { IPC_CHANNELS } from '../../shared/ipc-channels'

let koffi: any = null
try {
  // 可选: 未安装或平台不支持时 isDwmAvailable() 返回 false, 玻璃退化为 CSS
  koffi = require('koffi')
} catch {
  koffi = null
}

// ---- DWM / SetWindowCompositionAttribute FFI -------------------
const WCA_ACCENT_POLICY = 19
const ACCENT_DISABLED = 0
const ACCENT_ENABLE_BLURBEHIND = 3
const ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
const ACCENT_FLAG_APPLY_TO_CLIENT = 2
const DWMWA_USE_IMMERSIVE_DARK_MODE = 20
const DWMWA_WINDOW_CORNER_PREFERENCE = 33
const DWMWA_BORDER_COLOR = 34
const CORNER_DEFAULT = 0
const CORNER_DONOTROUND = 1
const CORNER_ROUND = 2
const CORNER_ROUNDSMALL = 3
const DWMWA_COLOR_NONE = 4294967294

let api: any = null
let apiFailed = false
function ensure(): any {
  if (api) return api
  if (apiFailed || !koffi) return null
  try {
    const user32 = koffi.load('user32.dll')
    const dwmapi = koffi.load('dwmapi.dll')
    const AccentPolicy = koffi.struct('ACCENT_POLICY', {
      AccentState: 'uint32',
      AccentFlags: 'uint32',
      GradientColor: 'uint32',
      AnimationId: 'uint32'
    })
    const WinCompAttrData = koffi.struct('WINDOWCOMPOSITIONATTRIBDATA', {
      Attrib: 'uint32',
      pvData: 'void *',
      cbData: 'size_t'
    })
    api = {
      SetWindowCompositionAttribute: user32.func(
        'SetWindowCompositionAttribute',
        'bool',
        ['uintptr_t', koffi.pointer(WinCompAttrData)]
      ),
      DwmSetWindowAttribute: dwmapi.func(
        'DwmSetWindowAttribute',
        'int',
        ['uintptr_t', 'uint32', 'void *', 'uint32']
      ),
      AccentPolicy
    }
    return api
  } catch (e) {
    apiFailed = true
    log.warn('[DWM] FFI 装配失败，玻璃材质将退化为 CSS 模拟:', e)
    return null
  }
}
function isDwmAvailable(): boolean {
  return ensure() !== null
}
function hwndOf(win: BrowserWindow): number {
  try {
    const buf = win.getNativeWindowHandle()
    if (buf.length >= 8) return Number(buf.readBigUInt64LE(0))
    return buf.readUInt32LE(0)
  } catch (e) {
    log.warn('[DWM] 取窗口句柄失败:', e)
    return 0
  }
}
function setAcrylic(hwnd: number, mode: string, tintAbgr = 2583691263): boolean {
  const a = ensure()
  if (!a || !hwnd) return false
  try {
    const state = mode === 'acrylic' ? ACCENT_ENABLE_ACRYLICBLURBEHIND : mode === 'blur' ? ACCENT_ENABLE_BLURBEHIND : ACCENT_DISABLED
    const policyBuf = koffi.alloc(a.AccentPolicy, 1)
    koffi.encode(policyBuf, a.AccentPolicy, {
      AccentState: state,
      AccentFlags: state === ACCENT_DISABLED ? 0 : ACCENT_FLAG_APPLY_TO_CLIENT,
      GradientColor: tintAbgr >>> 0,
      AnimationId: 0
    })
    const ok = a.SetWindowCompositionAttribute(hwnd, {
      Attrib: WCA_ACCENT_POLICY,
      pvData: policyBuf,
      cbData: koffi.sizeof(a.AccentPolicy)
    })
    if (!ok) log.warn(`[DWM] SetWindowCompositionAttribute 返回 false (mode=${mode})`)
    return ok
  } catch (e) {
    log.warn('[DWM] setAcrylic 失败:', e)
    return false
  }
}
function setDwmDword(hwnd: number, attr: number, value: number): boolean {
  const a = ensure()
  if (!a || !hwnd) return false
  try {
    const buf = koffi.alloc('uint32', 1)
    koffi.encode(buf, 'uint32', value >>> 0)
    const hr = a.DwmSetWindowAttribute(hwnd, attr, buf, 4)
    return hr === 0
  } catch (e) {
    log.warn(`[DWM] setDwmDword(attr=${attr}) 失败:`, e)
    return false
  }
}
function setCorner(hwnd: number, style: string): boolean {
  const map: Record<string, number> = { default: CORNER_DEFAULT, square: CORNER_DONOTROUND, round: CORNER_ROUND, roundSmall: CORNER_ROUNDSMALL }
  return setDwmDword(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, map[style])
}
function setImmersiveDark(hwnd: number, dark: boolean): boolean {
  return setDwmDword(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, dark ? 1 : 0)
}
function clearBorderColor(hwnd: number): boolean {
  return setDwmDword(hwnd, DWMWA_BORDER_COLOR, DWMWA_COLOR_NONE)
}

const CORNER_BY_ROLE: Record<string, string> = {
  sidebar: 'roundSmall',
  panel: 'round',
  floatball: 'square',
  transparent: 'square'
}

interface Target { win: BrowserWindow; role: string; corner: string }
let osBuild = 0
let targets: Target[] = []
let currentAppearance: AppearanceConfig = { ...DEFAULT_APPEARANCE }
let themeListenerBound = false

function currentTheme(): 'light' | 'dark' {
  return resolveTheme(currentAppearance.theme, nativeTheme.shouldUseDarkColors)
}
function effectiveMaterial(): string {
  if (process.platform !== 'win32') return 'none'
  return resolveMaterial(currentAppearance.material, osBuild, currentAppearance.liquidGlass)
}
function syncThemeSource(): void {
  nativeTheme.themeSource = currentAppearance.theme === 'auto' ? 'system' : (currentAppearance.theme as 'light' | 'dark')
}
function backgroundColorFor(role: string, material: string, theme: 'light' | 'dark'): string {
  if (role === 'floatball' || role === 'transparent') return '#00000000'
  if (material !== 'none') return '#00000000'
  return theme === 'dark' ? '#171b22' : '#ffffff'
}
/** 供窗口创建时取初始背景色 (ConfigService 可能尚未 init) */
export function initialBackgroundColor(role: string): string {
  let a: AppearanceConfig = DEFAULT_APPEARANCE
  try {
    a = clampAppearance(ConfigService.get().appearance, DEFAULT_APPEARANCE)
  } catch { /* 未初始化, 用默认 */ }
  const build = process.platform === 'win32' ? parseWindowsBuild(os.release()) : 0
  const mat = process.platform === 'win32' ? resolveMaterial(a.material, build, a.liquidGlass) : 'none'
  const theme = resolveTheme(a.theme, nativeTheme.shouldUseDarkColors)
  return backgroundColorFor(role, mat, theme)
}

function applyToTarget(t: Target): boolean {
  const { win, role } = t
  if (win.isDestroyed()) return false
  const theme = currentTheme()
  const material = effectiveMaterial()
  const dark = theme === 'dark'
  if (role === 'transparent' || role === 'floatball' || role === 'sidebar') {
    // 液态玻璃观感统一由页面 CSS 承载; 系统背板材质会铺满整个窗口矩形形成"方形底板",
    // 这里对悬浮球与侧边栏彻底清场恢复纯透明。
    try { win.setBackgroundColor('#00000000') } catch { /* ignore */ }
    try { if (typeof (win as any).setBackgroundMaterial === 'function') (win as any).setBackgroundMaterial('none') } catch { /* ignore */ }
    try { if (isDwmAvailable()) setAcrylic(hwndOf(win), 'off') } catch { /* ignore */ }
    return false
  }
  let applied = false
  try {
    win.setBackgroundColor(backgroundColorFor(role, material, theme))
  } catch (e) {
    log.warn('[Appearance] setBackgroundColor 失败:', e)
  }
  try {
    if (material === 'acrylic' || material === 'mica') {
      if (supportsNativeMaterial(osBuild) && typeof (win as any).setBackgroundMaterial === 'function') {
        ;(win as any).setBackgroundMaterial(material === 'mica' ? 'mica' : 'acrylic')
        applied = true
      } else if (isDwmAvailable()) {
        applied = setAcrylic(hwndOf(win), 'acrylic', accentGradientAbgr(currentAppearance, theme))
      }
    } else if (material === 'blur') {
      applied = setAcrylic(hwndOf(win), 'acrylic', accentGradientAbgr(currentAppearance, theme))
    } else {
      if (supportsNativeMaterial(osBuild) && typeof (win as any).setBackgroundMaterial === 'function') {
        ;(win as any).setBackgroundMaterial('none')
      }
      if (isDwmAvailable()) setAcrylic(hwndOf(win), 'off')
    }
  } catch (e) {
    log.warn(`[Appearance] 应用材质失败 (role=${role}):`, e)
  }
  if (process.platform === 'win32' && isDwmAvailable()) {
    const hwnd = hwndOf(win)
    if (hwnd) {
      setCorner(hwnd, t.corner)
      setImmersiveDark(hwnd, dark)
      clearBorderColor(hwnd)
    }
  }
  return applied
}

function broadcast(): void {
  const snap = AppearanceService.snapshot()
  for (const win of BrowserWindow.getAllWindows()) {
    if (win.isDestroyed()) continue
    try {
      win.webContents.send(IPC_CHANNELS['appearance:changed'], snap)
    } catch { /* ignore */ }
  }
}
function prune(): void {
  targets = targets.filter((t) => !t.win.isDestroyed())
}

export const AppearanceService = {
  init(): void {
    osBuild = process.platform === 'win32' ? parseWindowsBuild(os.release()) : 0
    currentAppearance = clampAppearance(ConfigService.get().appearance, DEFAULT_APPEARANCE)
    syncThemeSource()
    if (!themeListenerBound) {
      themeListenerBound = true
      nativeTheme.on('updated', () => {
        if (currentAppearance.theme !== 'auto') return
        log.info(`[Appearance] 系统主题变更 → ${nativeTheme.shouldUseDarkColors ? 'dark' : 'light'}`)
        this.reapply()
        broadcast()
      })
    }
    log.info(
      `[Appearance] 初始化完成 build=${osBuild} native=${supportsNativeMaterial(osBuild)} ffi=${isDwmAvailable()} material=${effectiveMaterial()} theme=${currentTheme()}`
    )
  },
  /** 注册一个需要玻璃处理的窗口, 必须在 ready-to-show 之后 (HWND 已创建) */
  register(win: BrowserWindow, role: string): void {
    prune()
    if (targets.some((t) => t.win === win)) return
    const t: Target = { win, role, corner: CORNER_BY_ROLE[role] || 'default' }
    targets.push(t)
    win.once('closed', () => {
      targets = targets.filter((x) => x.win !== win)
    })
    applyToTarget(t)
  },
  reapply(): void {
    prune()
    for (const t of targets) applyToTarget(t)
  },
  snapshot(): AppearanceSnapshot {
    return {
      appearance: { ...currentAppearance },
      theme: currentTheme(),
      effectiveMaterial: effectiveMaterial(),
      osBuild,
      nativeMaterial: supportsNativeMaterial(osBuild)
    }
  },
  /** 更新外观 (patch 只需带要改的字段), 立即落盘 */
  set(patch: Partial<AppearanceConfig>): AppearanceSnapshot {
    const next = clampAppearance({ ...currentAppearance, ...patch }, DEFAULT_APPEARANCE)
    const themeModeChanged = next.theme !== currentAppearance.theme
    currentAppearance = next
    ConfigService.set('appearance', { ...next })
    if (themeModeChanged) syncThemeSource()
    this.reapply()
    broadcast()
    return this.snapshot()
  },
  build(): number {
    return osBuild
  }
}

// 让 screen 类型被引用 (避免未使用告警的同时保留未来扩展点)
void screen

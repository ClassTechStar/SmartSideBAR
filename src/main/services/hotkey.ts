// services/hotkey.ts - 全局快捷键槽位管理 (P2-2)
// 管理多个全局热键槽位: 截图 / 批注 / 长截图。
// 注册失败 (被其他应用占用) 时返回可用替代组合, 供设置 UI 提示。

import { globalShortcut } from 'electron'
import log from 'electron-log'

export interface HotkeySlot {
  id: string
  accelerator: string
  action: () => void
}

export interface RegisterResult {
  ok: boolean
  suggestions: string[]
}

const slots = new Map<string, HotkeySlot>()

// 探测某个加速键是否可注册 (注册空回调后立即注销, 不产生副作用)
function isAvailable(acc: string): boolean {
  try {
    const ok = globalShortcut.register(acc, () => {})
    if (ok) globalShortcut.unregister(acc)
    return ok
  } catch {
    return false
  }
}

// 生成替代组合: 尝试不同修饰键 + 字母, 返回可用的前 count 个
function suggestAlternatives(base: string, count = 3): string[] {
  const modifiers = ['Ctrl+Alt+Shift', 'Ctrl+Alt', 'Ctrl+Shift', 'Alt+Shift']
  const letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('')
  const last = (base.match(/[A-Za-z]$/)?.[0] || 'A').toUpperCase()
  const taken = new Set([...slots.values()].map(s => s.accelerator))
  const out: string[] = []
  for (const mod of modifiers) {
    for (const k of letters) {
      if (k === last) continue
      const cand = `${mod}+${k}`
      if (taken.has(cand)) continue
      if (isAvailable(cand)) {
        out.push(cand)
        if (out.length >= count) return out
      }
    }
  }
  return out
}

export const HotkeyService = {
  /** 注册/更新一个槽位。accelerator 为空表示停用该槽位。 */
  register(id: string, accelerator: string | undefined, action: () => void): RegisterResult {
    if (!accelerator) return { ok: false, suggestions: [] }

    // 注销该 id 的旧绑定 (仅当键值变化时), 避免重复注册
    const old = slots.get(id)
    if (old && old.accelerator !== accelerator) {
      try { globalShortcut.unregister(old.accelerator) } catch { /* ignore */ }
      slots.delete(id)
    }

    // 同键被其他槽位占用 (应用内部冲突)
    const conflict = [...slots.values()].some(s => s.id !== id && s.accelerator === accelerator)
    if (conflict) {
      log.warn(`[Hotkey] ${id}: "${accelerator}" 已被其他槽位占用`)
      return { ok: false, suggestions: [] }
    }

    try {
      const ok = globalShortcut.register(accelerator, action)
      if (ok) {
        slots.set(id, { id, accelerator, action })
        log.info(`[Hotkey] ${id} -> ${accelerator}`)
        return { ok: true, suggestions: [] }
      }
      const suggestions = suggestAlternatives(accelerator)
      log.warn(`[Hotkey] ${id} register failed (可能已被其他应用占用): ${accelerator}; 替代: ${suggestions.join(', ') || '无'}`)
      return { ok: false, suggestions }
    } catch (e: any) {
      log.warn(`[Hotkey] ${id} register error: ${e.message}`)
      return { ok: false, suggestions: suggestAlternatives(accelerator) }
    }
  },

  getState(): Array<{ id: string; accelerator: string }> {
    return [...slots.values()].map(s => ({ id: s.id, accelerator: s.accelerator }))
  },

  unregisterAll(): void {
    globalShortcut.unregisterAll()
    slots.clear()
  }
}

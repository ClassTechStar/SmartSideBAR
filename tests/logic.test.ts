// C3: 纯逻辑单测 —— floatball-layout / appearance / hotkey 冲突检测
// 这些模块无 Electron 依赖, 可直接测试
import { describe, it, expect } from 'vitest'
import {
  computeFanLayout,
  collapsedLayout,
  resolveBallPosition,
  clampToArea,
  snapToEdges,
  toRelativePosition,
  clampFloatBall,
  DEFAULT_FLOATBALL,
  FLOATBALL_LIMITS,
  FAN_RADIUS_MIN,
  FAN_RADIUS_MAX
} from '../src/shared/floatball-layout'
import {
  clampAppearance,
  normalizeAccent,
  DEFAULT_APPEARANCE,
  GLASS_LIMITS
} from '../src/shared/appearance'

describe('floatball-layout: clampFloatBall', () => {
  it('should return defaults for empty input', () => {
    const result = clampFloatBall({})
    expect(result.size).toBe(DEFAULT_FLOATBALL.size)
    expect(result.enabled).toBe(DEFAULT_FLOATBALL.enabled)
  })

  it('should clamp size to valid range', () => {
    expect(clampFloatBall({ size: 10 }).size).toBe(FLOATBALL_LIMITS.size.min)
    expect(clampFloatBall({ size: 200 }).size).toBe(FLOATBALL_LIMITS.size.max)
    expect(clampFloatBall({ size: 56 }).size).toBe(56)
  })

  it('should clamp idleOpacity', () => {
    expect(clampFloatBall({ idleOpacity: 0 }).idleOpacity).toBe(FLOATBALL_LIMITS.idleOpacity.min)
    expect(clampFloatBall({ idleOpacity: 2 }).idleOpacity).toBe(FLOATBALL_LIMITS.idleOpacity.max)
  })

  it('should handle NaN gracefully', () => {
    expect(clampFloatBall({ size: NaN }).size).toBe(DEFAULT_FLOATBALL.size)
    expect(clampFloatBall({ idleOpacity: NaN }).idleOpacity).toBe(DEFAULT_FLOATBALL.idleOpacity)
  })
})

describe('floatball-layout: clampToArea', () => {
  const area = { x: 0, y: 0, width: 1920, height: 1080 }

  it('should keep position if within bounds', () => {
    const result = clampToArea({ x: 100, y: 100 }, { width: 56, height: 56 }, area)
    expect(result.x).toBe(100)
    expect(result.y).toBe(100)
  })

  it('should clamp to left edge', () => {
    const result = clampToArea({ x: -50, y: 100 }, { width: 56, height: 56 }, area)
    expect(result.x).toBe(0)
  })

  it('should clamp to right edge', () => {
    const result = clampToArea({ x: 1900, y: 100 }, { width: 56, height: 56 }, area)
    expect(result.x).toBe(1920 - 56)
  })

  it('should clamp to top edge', () => {
    const result = clampToArea({ x: 100, y: -10 }, { width: 56, height: 56 }, area)
    expect(result.y).toBe(0)
  })

  it('should clamp to bottom edge', () => {
    const result = clampToArea({ x: 100, y: 1060 }, { width: 56, height: 56 }, area)
    expect(result.y).toBe(1080 - 56)
  })
})

describe('floatball-layout: snapToEdges', () => {
  const area = { x: 0, y: 0, width: 1920, height: 1080 }

  it('should snap to right edge when close', () => {
    const result = snapToEdges({ x: 1920 - 56 - 10, y: 500 }, { width: 56, height: 56 }, area, 24)
    expect(result.x).toBe(1920 - 56)
    expect(result.edge).toBe('right')
  })

  it('should snap to left edge when close', () => {
    const result = snapToEdges({ x: 10, y: 500 }, { width: 56, height: 56 }, area, 24)
    expect(result.x).toBe(0)
    expect(result.edge).toBe('left')
  })

  it('should not snap when far from edges', () => {
    const result = snapToEdges({ x: 500, y: 500 }, { width: 56, height: 56 }, area, 24)
    expect(result.edge).toBeNull()
    expect(result.x).toBe(500)
  })
})

describe('floatball-layout: resolveBallPosition', () => {
  const area = { x: 0, y: 0, width: 1920, height: 1080 }

  it('should place ball on right side when sidebar is right', () => {
    const cfg = { ...DEFAULT_FLOATBALL, x: -1, y: -1 }
    const pos = resolveBallPosition(cfg, area, 56, 'right')
    expect(pos.x).toBeGreaterThan(area.width / 2)
  })

  it('should place ball on left side when sidebar is left', () => {
    const cfg = { ...DEFAULT_FLOATBALL, x: -1, y: -1 }
    const pos = resolveBallPosition(cfg, area, 56, 'left')
    expect(pos.x).toBeLessThan(area.width / 2)
  })

  it('should use persisted relative position when valid', () => {
    const cfg = { ...DEFAULT_FLOATBALL, x: 0.8, y: 0.5 }
    const pos = resolveBallPosition(cfg, area, 56, 'right')
    // 0.8 * 1920 = 1536, clamped within area
    expect(pos.x).toBeGreaterThanOrEqual(0)
    expect(pos.x).toBeLessThanOrEqual(area.width - 56)
  })
})

describe('floatball-layout: computeFanLayout', () => {
  it('should return valid window bounds', () => {
    const result = computeFanLayout({
      ball: { x: 100, y: 100 },
      ballSize: 56,
      area: { x: 0, y: 0, width: 1920, height: 1080 },
      count: 6,
      uiScale: 1
    })
    expect(result.window.width).toBeGreaterThan(0)
    expect(result.window.height).toBeGreaterThan(0)
  })

  it('should handle zero actions', () => {
    const result = computeFanLayout({
      ball: { x: 100, y: 100 },
      ballSize: 56,
      area: { x: 0, y: 0, width: 1920, height: 1080 },
      count: 0,
      uiScale: 1
    })
    // 无菜单项时窗口应等于球大小
    expect(result.window.width).toBeGreaterThanOrEqual(56)
  })

  it('should respect radius bounds', () => {
    const result = computeFanLayout({
      ball: { x: 100, y: 100 },
      ballSize: 56,
      area: { x: 0, y: 0, width: 1920, height: 1080 },
      count: 8,
      uiScale: 1
    })
    // 窗口应包含球 + 菜单项半径
    expect(result.window.width).toBeGreaterThanOrEqual(FAN_RADIUS_MIN)
  })
})

describe('floatball-layout: toRelativePosition', () => {
  it('should convert absolute to area-relative offset', () => {
    // toRelativePosition 返回相对 workArea 原点的偏移 (非 0-1 比例)
    const rel = toRelativePosition({ x: 960, y: 540 }, { x: 0, y: 0, width: 1920, height: 1080 })
    expect(rel.x).toBe(960)
    expect(rel.y).toBe(540)
  })

  it('should subtract area origin', () => {
    const rel = toRelativePosition({ x: 1960, y: 640 }, { x: 1000, y: 100, width: 1920, height: 1080 })
    expect(rel.x).toBe(960)
    expect(rel.y).toBe(540)
  })

  it('should clamp negative to zero', () => {
    const rel = toRelativePosition({ x: -10, y: -5 }, { x: 0, y: 0, width: 1920, height: 1080 })
    expect(rel.x).toBe(0)
    expect(rel.y).toBe(0)
  })
})

describe('floatball-layout: collapsedLayout', () => {
  it('should return collapsed state with zero radius', () => {
    const layout = collapsedLayout(56)
    expect(layout.expanded).toBe(false)
    expect(layout.radius).toBe(0)
    expect(layout.ballSize).toBe(56)
  })
})

describe('appearance: clampAppearance', () => {
  it('should return defaults for empty input', () => {
    const result = clampAppearance({})
    expect(result.blur).toBe(DEFAULT_APPEARANCE.blur)
    expect(result.theme).toBe(DEFAULT_APPEARANCE.theme)
  })

  it('should clamp blur to range', () => {
    expect(clampAppearance({ blur: 0 }).blur).toBe(GLASS_LIMITS.blur.min)
    expect(clampAppearance({ blur: 100 }).blur).toBe(GLASS_LIMITS.blur.max)
  })

  it('should clamp opacity', () => {
    expect(clampAppearance({ opacity: 0 }).opacity).toBe(GLASS_LIMITS.opacity.min)
    expect(clampAppearance({ opacity: 2 }).opacity).toBe(GLASS_LIMITS.opacity.max)
  })

  it('should validate theme', () => {
    expect(clampAppearance({ theme: 'light' }).theme).toBe('light')
    expect(clampAppearance({ theme: 'dark' }).theme).toBe('dark')
    expect(clampAppearance({ theme: 'auto' }).theme).toBe('auto')
    expect(clampAppearance({ theme: 'blue' }).theme).toBe('auto') // fallback
  })

  it('should validate material', () => {
    expect(clampAppearance({ material: 'mica' }).material).toBe('mica')
    expect(clampAppearance({ material: 'invalid' }).material).toBe('acrylic') // fallback
  })

  it('should validate accent color', () => {
    expect(clampAppearance({ accent: '#FF0000' }).accent).toBe('#ff0000')
    expect(clampAppearance({ accent: 'not-a-color' }).accent).toBe(DEFAULT_APPEARANCE.accent)
  })
})

describe('appearance: normalizeAccent', () => {
  it('should expand 3-digit hex', () => {
    expect(normalizeAccent('#abc')).toBe('#aabbcc')
  })

  it('should lowercase 6-digit hex', () => {
    expect(normalizeAccent('#AABBCC')).toBe('#aabbcc')
  })

  it('should return fallback for invalid input', () => {
    expect(normalizeAccent('red', '#2B6EE0')).toBe('#2B6EE0')
    expect(normalizeAccent(null, '#2B6EE0')).toBe('#2B6EE0')
  })
})

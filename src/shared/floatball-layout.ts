// shared/floatball-layout.ts - 悬浮球扇形菜单布局算法 (从 v1.1 编译产物重建)
// 主进程 computeFanLayout 决定窗口尺寸/位置; 渲染层 fanItemOffset 摆放菜单项。

export const FAN_START_DEG = 4
export const FAN_SWEEP_DEG = 92
export const FAN_ITEM_SIZE = 46
export const FAN_ITEM_GAP = 12
export const FAN_MARGIN = 12
export const FAN_RADIUS_MIN = 96
export const FAN_RADIUS_MAX = 260

export const FLOATBALL_ACTIONS = [
  'capture',
  'annotate',
  'longshot',
  'record',
  'ime',
  'taskmgr',
  'sidebar',
  'settings'
] as const

export interface FloatBallConfig {
  enabled: boolean
  size: number
  idleOpacity: number
  idleDelayMs: number
  snapThreshold: number
  x: number
  y: number
  actions: string[]
  hotkey: string
  doubleClick: 'toggleSidebar' | 'capture' | 'none'
}

export const DEFAULT_FLOATBALL: FloatBallConfig = {
  enabled: true,
  size: 56,
  idleOpacity: 0.55,
  idleDelayMs: 4000,
  snapThreshold: 24,
  x: -1,
  y: -1,
  actions: ['capture', 'annotate', 'record', 'ime', 'longshot', 'sidebar'],
  hotkey: 'Alt+Q',
  doubleClick: 'toggleSidebar'
}

export const FLOATBALL_LIMITS = {
  size: { min: 40, max: 96 },
  idleOpacity: { min: 0.15, max: 1 },
  idleDelayMs: { min: 0, max: 60000 },
  snapThreshold: { min: 0, max: 120 }
} as const

function clampNum(v: unknown, range: { min: number; max: number }, fallback: number): number {
  const n = typeof v === 'number' ? v : Number(v)
  if (!Number.isFinite(n)) return fallback
  return Math.min(range.max, Math.max(range.min, n))
}

export function clampFloatBall(input: unknown, base: FloatBallConfig = DEFAULT_FLOATBALL): FloatBallConfig {
  const src = (input || {}) as Record<string, unknown>
  const rawActions = Array.isArray(src.actions) ? (src.actions as string[]) : base.actions
  const actions = Array.from(new Set(rawActions)).filter(a => (FLOATBALL_ACTIONS as readonly string[]).includes(a)).slice(0, 8)
  const dc = src.doubleClick
  return {
    enabled: typeof src.enabled === 'boolean' ? src.enabled : base.enabled,
    size: Math.round(clampNum(src.size, FLOATBALL_LIMITS.size, base.size)),
    idleOpacity: clampNum(src.idleOpacity, FLOATBALL_LIMITS.idleOpacity, base.idleOpacity),
    idleDelayMs: Math.round(clampNum(src.idleDelayMs, FLOATBALL_LIMITS.idleDelayMs, base.idleDelayMs)),
    snapThreshold: Math.round(clampNum(src.snapThreshold, FLOATBALL_LIMITS.snapThreshold, base.snapThreshold)),
    x: typeof src.x === 'number' && Number.isFinite(src.x) ? Math.round(src.x) : base.x,
    y: typeof src.y === 'number' && Number.isFinite(src.y) ? Math.round(src.y) : base.y,
    actions: actions.length > 0 ? actions : [...DEFAULT_FLOATBALL.actions],
    hotkey: typeof src.hotkey === 'string' ? (src.hotkey as string).trim() : base.hotkey,
    doubleClick: dc === 'toggleSidebar' || dc === 'capture' || dc === 'none' ? dc : base.doubleClick
  }
}

export interface RectLike { x: number; y: number; width: number; height: number }

export function clampToArea(pos: { x: number; y: number }, size: { width: number; height: number }, area: RectLike) {
  const maxX = area.x + area.width - size.width
  const maxY = area.y + area.height - size.height
  return {
    x: Math.round(Math.min(Math.max(pos.x, area.x), Math.max(area.x, maxX))),
    y: Math.round(Math.min(Math.max(pos.y, area.y), Math.max(area.y, maxY)))
  }
}

export function snapToEdges(
  pos: { x: number; y: number },
  size: { width: number; height: number },
  area: RectLike,
  threshold: number
): { x: number; y: number; edge: string | null } {
  const p = clampToArea(pos, size, area)
  if (!(threshold > 0)) return { ...p, edge: null }
  const dLeft = p.x - area.x
  const dRight = area.x + area.width - (p.x + size.width)
  const dTop = p.y - area.y
  const dBottom = area.y + area.height - (p.y + size.height)
  const nearest = Math.min(dLeft, dRight, dTop, dBottom)
  if (nearest > threshold) return { ...p, edge: null }
  if (nearest === dLeft) return { x: area.x, y: p.y, edge: 'left' }
  if (nearest === dRight) return { x: area.x + area.width - size.width, y: p.y, edge: 'right' }
  if (nearest === dTop) return { x: p.x, y: area.y, edge: 'top' }
  return { x: p.x, y: area.y + area.height - size.height, edge: 'bottom' }
}

export function defaultBallPosition(area: RectLike, ballDip: number, side: string) {
  const inset = Math.round(ballDip * 0.28)
  const x = side === 'right' ? area.x + area.width - ballDip - inset : area.x + inset
  const y = area.y + Math.round(area.height * 0.62)
  return clampToArea({ x, y }, { width: ballDip, height: ballDip }, area)
}

/** cfg.x/y 为相对 workArea 的偏移; <0 表示未设置 -> 默认位置 */
export function resolveBallPosition(cfg: { x: number; y: number }, area: RectLike, ballDip: number, side: string) {
  if (cfg.x < 0 || cfg.y < 0) return defaultBallPosition(area, ballDip, side)
  return clampToArea({ x: area.x + cfg.x, y: area.y + cfg.y }, { width: ballDip, height: ballDip }, area)
}

export function toRelativePosition(pos: { x: number; y: number }, area: RectLike) {
  return { x: Math.max(0, Math.round(pos.x - area.x)), y: Math.max(0, Math.round(pos.y - area.y)) }
}

export function fanRadius(count: number, itemSize = FAN_ITEM_SIZE, gap = FAN_ITEM_GAP, sweepDeg = FAN_SWEEP_DEG): number {
  if (count <= 1) return FAN_RADIUS_MIN
  const step = (sweepDeg * Math.PI) / 180 / (count - 1)
  const needed = (itemSize + gap) / step
  return Math.round(Math.min(FAN_RADIUS_MAX, Math.max(FAN_RADIUS_MIN, needed)))
}

export function fanItemOffset(
  index: number,
  count: number,
  radius: number,
  dir: { x: number; y: number },
  startDeg = FAN_START_DEG,
  sweepDeg = FAN_SWEEP_DEG
): { x: number; y: number } {
  const startRad = (startDeg * Math.PI) / 180
  const sweepRad = (sweepDeg * Math.PI) / 180
  const angle = count <= 1 ? startRad + sweepRad / 2 : startRad + (sweepRad / (count - 1)) * index
  return {
    x: dir.x * radius * Math.cos(angle),
    y: dir.y * radius * Math.sin(angle)
  }
}

export interface FloatBallLayout {
  expanded: boolean
  ballSize: number
  ballOffset: { x: number; y: number }
  dir: { x: number; y: number }
  radius: number
  itemSize: number
}

export function computeFanLayout(params: {
  ball: { x: number; y: number }
  ballSize: number
  area: RectLike
  count: number
  uiScale: number
  itemSize?: number
  gap?: number
}): { layout: FloatBallLayout; window: { x: number; y: number; width: number; height: number } } {
  const { ball, ballSize, area, count, uiScale } = params
  const itemSize = params.itemSize ?? FAN_ITEM_SIZE
  const gap = params.gap ?? FAN_ITEM_GAP
  const scale = uiScale > 0 ? uiScale : 1
  const ballDip = Math.round(ballSize * scale)
  const radius = fanRadius(count, itemSize, gap)
  const roomLeft = ball.x - area.x
  const roomRight = area.x + area.width - (ball.x + ballDip)
  const roomTop = ball.y - area.y
  const roomBottom = area.y + area.height - (ball.y + ballDip)
  const dir = {
    x: roomRight >= roomLeft ? 1 : -1,
    y: roomBottom >= roomTop ? 1 : -1
  }
  const half = ballSize / 2
  let minX = -half
  let maxX = half
  let minY = -half
  let maxY = half
  const itemHalf = itemSize / 2
  for (let i = 0; i < count; i++) {
    const o = fanItemOffset(i, count, radius, dir)
    minX = Math.min(minX, o.x - itemHalf)
    maxX = Math.max(maxX, o.x + itemHalf)
    minY = Math.min(minY, o.y - itemHalf)
    maxY = Math.max(maxY, o.y + itemHalf)
  }
  const panelCssW = Math.ceil(maxX - minX + FAN_MARGIN * 2)
  const panelCssH = Math.ceil(maxY - minY + FAN_MARGIN * 2)
  const centerX = -minX + FAN_MARGIN
  const centerY = -minY + FAN_MARGIN
  const ballOffset = { x: Math.round(centerX - half), y: Math.round(centerY - half) }
  const winW = Math.round(panelCssW * scale)
  const winH = Math.round(panelCssH * scale)
  const desired = {
    x: ball.x - Math.round(ballOffset.x * scale),
    y: ball.y - Math.round(ballOffset.y * scale)
  }
  const placed = clampToArea(desired, { width: winW, height: winH }, area)
  const compensated = {
    x: ballOffset.x + Math.round((desired.x - placed.x) / scale),
    y: ballOffset.y + Math.round((desired.y - placed.y) / scale)
  }
  const safeOffset = {
    x: Math.min(Math.max(compensated.x, 0), Math.max(0, panelCssW - ballSize)),
    y: Math.min(Math.max(compensated.y, 0), Math.max(0, panelCssH - ballSize))
  }
  return {
    layout: { expanded: true, ballSize, ballOffset: safeOffset, dir, radius, itemSize },
    window: { x: placed.x, y: placed.y, width: winW, height: winH }
  }
}

export function collapsedLayout(ballSize: number, itemSize = FAN_ITEM_SIZE): FloatBallLayout {
  return { expanded: false, ballSize, ballOffset: { x: 0, y: 0 }, dir: { x: 1, y: 1 }, radius: 0, itemSize }
}

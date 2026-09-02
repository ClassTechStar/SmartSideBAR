// shared/appearance.ts - 液态玻璃外观 token 计算 (从 v1.1 编译产物重建)
// 主进程 AppearanceService 与渲染进程 App.vue 共用。

export interface AppearanceConfig {
  theme: 'light' | 'dark' | 'auto'
  liquidGlass: boolean
  material: 'acrylic' | 'mica' | 'blur' | 'none'
  blur: number
  opacity: number
  saturate: number
  refraction: number
  specular: number
  aberration: number
  accent: string
  radius: number
  reduceMotion: boolean
}

export const GLASS_LIMITS = {
  blur: { min: 8, max: 40 },
  opacity: { min: 0.3, max: 0.96 },
  saturate: { min: 1, max: 2.2 },
  refraction: { min: 0, max: 40 },
  specular: { min: 0, max: 1 },
  aberration: { min: 0, max: 8 },
  radius: { min: 0, max: 28 }
} as const

export const DEFAULT_APPEARANCE: AppearanceConfig = {
  theme: 'auto',
  liquidGlass: true,
  material: 'acrylic',
  blur: 22,
  opacity: 0.62,
  saturate: 1.6,
  refraction: 14,
  specular: 0.55,
  aberration: 2,
  accent: '#2B6EE0',
  radius: 16,
  reduceMotion: false
}

export const THEME_MODES = ['light', 'dark', 'auto'] as const
export const MATERIALS = ['acrylic', 'mica', 'blur', 'none'] as const

function clampNum(v: unknown, range: { min: number; max: number }, fallback: number): number {
  const n = typeof v === 'number' ? v : Number(v)
  if (!Number.isFinite(n)) return fallback
  return Math.min(range.max, Math.max(range.min, n))
}

export function normalizeAccent(input: unknown, fallback: string = DEFAULT_APPEARANCE.accent): string {
  if (typeof input !== 'string') return fallback
  const s = input.trim()
  if (/^#[0-9a-fA-F]{3}$/.test(s)) {
    return `#${s[1]}${s[1]}${s[2]}${s[2]}${s[3]}${s[3]}`.toLowerCase()
  }
  if (/^#[0-9a-fA-F]{6}$/.test(s)) return s.toLowerCase()
  return fallback
}

export function clampAppearance(input: unknown, base: AppearanceConfig = DEFAULT_APPEARANCE): AppearanceConfig {
  const src = (input || {}) as Record<string, unknown>
  return {
    theme: THEME_MODES.includes(src.theme as any) ? (src.theme as AppearanceConfig['theme']) : base.theme,
    liquidGlass: typeof src.liquidGlass === 'boolean' ? src.liquidGlass : base.liquidGlass,
    material: MATERIALS.includes(src.material as any) ? (src.material as AppearanceConfig['material']) : base.material,
    blur: clampNum(src.blur, GLASS_LIMITS.blur, base.blur),
    opacity: clampNum(src.opacity, GLASS_LIMITS.opacity, base.opacity),
    saturate: clampNum(src.saturate, GLASS_LIMITS.saturate, base.saturate),
    refraction: clampNum(src.refraction, GLASS_LIMITS.refraction, base.refraction),
    specular: clampNum(src.specular, GLASS_LIMITS.specular, base.specular),
    aberration: clampNum(src.aberration, GLASS_LIMITS.aberration, base.aberration),
    accent: normalizeAccent(src.accent, base.accent),
    radius: clampNum(src.radius, GLASS_LIMITS.radius, base.radius),
    reduceMotion: typeof src.reduceMotion === 'boolean' ? src.reduceMotion : base.reduceMotion
  }
}

export function hexToRgb(hex: string): { r: number; g: number; b: number } {
  const s = normalizeAccent(hex)
  return {
    r: parseInt(s.slice(1, 3), 16),
    g: parseInt(s.slice(3, 5), 16),
    b: parseInt(s.slice(5, 7), 16)
  }
}

export function relativeLuminance(hex: string): number {
  const { r, g, b } = hexToRgb(hex)
  const lin = (c: number) => {
    const v = c / 255
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4)
  }
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)
}

export function onAccentColor(hex: string): string {
  return relativeLuminance(hex) > 0.45 ? '#101418' : '#ffffff'
}

export function shade(hex: string, amount: number): string {
  const { r, g, b } = hexToRgb(hex)
  const f = (c: number) =>
    Math.round(amount >= 0 ? c + (255 - c) * amount : c * (1 + amount)).toString(16).padStart(2, '0')
  return `#${f(r)}${f(g)}${f(b)}`
}

/** 计算注入 <html style> 的全部 --lg-* CSS 变量 */
export function glassCssVars(
  a: unknown,
  theme: 'light' | 'dark',
  effectiveMaterial: string = 'none'
): Record<string, string> {
  const cfg = clampAppearance(a)
  const dark = theme === 'dark'
  const hasOsBlur = effectiveMaterial !== 'none'
  const tintAlpha = hasOsBlur ? cfg.opacity * 0.55 : cfg.opacity
  const glassRgb = dark ? '22, 26, 33' : '255, 255, 255'
  const veilRgb = dark ? '10, 12, 16' : '236, 241, 248'
  const accent = normalizeAccent(cfg.accent)
  return {
    '--lg-enabled': cfg.liquidGlass ? '1' : '0',
    '--lg-blur': `${cfg.blur}px`,
    '--lg-blur-strong': `${Math.round(cfg.blur * 1.6)}px`,
    '--lg-sat': `${cfg.saturate}`,
    '--lg-radius': `${cfg.radius}px`,
    '--lg-radius-sm': `${Math.max(4, Math.round(cfg.radius * 0.6))}px`,
    '--lg-radius-pill': '999px',
    '--lg-refraction': `${cfg.refraction}`,
    '--lg-specular': `${cfg.specular}`,
    '--lg-aberration': `${cfg.aberration}`,
    // 玻璃主体
    '--lg-tint': `rgba(${glassRgb}, ${tintAlpha.toFixed(3)})`,
    '--lg-tint-weak': `rgba(${glassRgb}, ${(tintAlpha * 0.55).toFixed(3)})`,
    '--lg-tint-strong': `rgba(${glassRgb}, ${Math.min(0.98, tintAlpha * 1.25).toFixed(3)})`,
    '--lg-veil': `rgba(${veilRgb}, ${dark ? 0.72 : 0.86})`,
    // 玻璃关闭时的不透明兜底色（投影/低配场景）
    '--lg-solid': dark ? '#171b22' : '#ffffff',
    // 边缘光 / 镜面高光
    '--lg-rim': dark ? `rgba(255, 255, 255, ${(0.14 + cfg.specular * 0.22).toFixed(3)})` : `rgba(255, 255, 255, ${(0.5 + cfg.specular * 0.45).toFixed(3)})`,
    '--lg-rim-bottom': dark ? 'rgba(255, 255, 255, 0.06)' : 'rgba(255, 255, 255, 0.28)',
    '--lg-hairline': dark ? 'rgba(255, 255, 255, 0.10)' : 'rgba(16, 24, 40, 0.08)',
    '--lg-specular-color': dark ? `rgba(255, 255, 255, ${(cfg.specular * 0.3).toFixed(3)})` : `rgba(255, 255, 255, ${(cfg.specular * 0.85).toFixed(3)})`,
    // 投影：亮色用冷灰，暗色用纯黑加深
    '--lg-shadow': dark ? '0 18px 48px rgba(0, 0, 0, 0.55), 0 2px 8px rgba(0, 0, 0, 0.4)' : '0 18px 44px rgba(16, 32, 64, 0.16), 0 2px 8px rgba(16, 32, 64, 0.08)',
    '--lg-shadow-sm': dark ? '0 6px 18px rgba(0, 0, 0, 0.45)' : '0 6px 18px rgba(16, 32, 64, 0.12)',
    // 文本 / 分隔线
    '--lg-text': dark ? '#eef2f8' : '#141a22',
    '--lg-text-secondary': dark ? 'rgba(238, 242, 248, 0.66)' : 'rgba(20, 26, 34, 0.62)',
    '--lg-text-disabled': dark ? 'rgba(238, 242, 248, 0.36)' : 'rgba(20, 26, 34, 0.34)',
    '--lg-divider': dark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(16, 24, 40, 0.07)',
    '--lg-hover': dark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(16, 32, 64, 0.05)',
    // 主题色族
    '--lg-accent': accent,
    '--lg-accent-hover': dark ? shade(accent, 0.16) : shade(accent, -0.12),
    '--lg-accent-soft': dark ? `${accent}33` : `${accent}1f`,
    '--lg-accent-on': onAccentColor(accent),
    // 动效时长（降动效时归零，CSS 里统一引用）
    '--lg-anim': cfg.reduceMotion ? '0ms' : '220ms',
    '--lg-anim-fast': cfg.reduceMotion ? '0ms' : '120ms',
    '--lg-spring': cfg.reduceMotion ? 'linear' : 'cubic-bezier(0.34, 1.46, 0.44, 0.96)'
  }
}

export function resolveTheme(mode: string, systemDark: boolean): 'light' | 'dark' {
  if (mode === 'light') return 'light'
  if (mode === 'dark') return 'dark'
  return systemDark ? 'dark' : 'light'
}

/** 请求材质 -> 实际生效材质 (按 Windows build 降级) */
export function resolveMaterial(requested: string, osBuild: number, liquidGlass: boolean): string {
  if (!liquidGlass) return 'none'
  if (requested === 'none') return 'none'
  if (osBuild >= 22621) return requested === 'blur' ? 'acrylic' : requested
  if (osBuild >= 17134) return 'blur'
  return 'none'
}

export function supportsNativeMaterial(osBuild: number): boolean {
  return osBuild >= 22621
}

export function parseWindowsBuild(release: string): number {
  const m = /^\d+\.\d+\.(\d+)/.exec(release || '')
  if (!m) return 0
  const n = Number(m[1])
  return Number.isFinite(n) ? n : 0
}

/** 亚克力背板的 ABGR 着色 (与 v1.1 编译产物一致) */
export function accentGradientAbgr(a: unknown, theme: 'light' | 'dark'): number {
  const cfg = clampAppearance(a)
  const alpha = Math.round(Math.min(0.85, cfg.opacity * 0.75) * 255)
  const base = theme === 'dark' ? { r: 20, g: 24, b: 30 } : { r: 250, g: 252, b: 255 }
  return ((alpha << 24) | (base.b << 16) | (base.g << 8) | base.r) >>> 0
}

export interface AppearanceSnapshot {
  appearance: AppearanceConfig
  theme: 'light' | 'dark'
  effectiveMaterial: string
  osBuild: number
  nativeMaterial: boolean
}

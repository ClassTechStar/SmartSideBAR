// services/display.ts - 显示服务 (坐标唯一事实源)

import { screen, BrowserWindow } from 'electron'
import log from 'electron-log'
import type { DisplayInfo, Rect, RectPx, SidekickConfig } from '../../shared/types'

let displays: DisplayInfo[] = []
let targetDisplay: DisplayInfo | null = null

function buildDisplayInfo(): DisplayInfo[] {
  return screen.getAllDisplays().map(d => ({
    id: d.id,
    name: `Display ${d.id}`,
    bounds: { x: d.bounds.x, y: d.bounds.y, width: d.bounds.width, height: d.bounds.height },
    workArea: { x: d.workArea.x, y: d.workArea.y, width: d.workArea.width, height: d.workArea.height },
    sizePx: { w: d.size.width, h: d.size.height },
    scaleFactor: d.scaleFactor,
    primary: d.id === screen.getPrimaryDisplay().id
  }))
}

export const DisplayService = {
  async init(config: SidekickConfig): Promise<void> {
    displays = buildDisplayInfo()
    this.selectTarget(config)

    // E7 修复: 显示器变更监听统一由 main.ts 的防抖处理器调用 refresh(),
    // 此处不再重复注册 screen 事件监听器

    log.info(`[Display] Initialized with ${displays.length} display(s)`)
  },

  /** 显示器配置变化时刷新缓存并重新选择目标 (由 main.ts 防抖调用) */
  refresh(config: SidekickConfig): void {
    displays = buildDisplayInfo()
    this.selectTarget(config)
    log.info(`[Display] Refreshed: ${displays.length} display(s), target=${this.sidebarTarget().id}`)
  },

  selectTarget(config: SidekickConfig): void {
    const primary = displays.find(d => d.primary) || displays[0]
    if (!primary) return

    const cfgMonitor = config.display.sidebarMonitor
    if (cfgMonitor === 'primary') {
      targetDisplay = primary
    } else {
      // 尝试找指定显示器,否则回退 primary
      targetDisplay = displays.find(d => d.id === parseInt(cfgMonitor)) || primary
    }

    log.info(`[Display] Target display: ${targetDisplay.id} (${targetDisplay.workArea.width}x${targetDisplay.workArea.height} @ ${targetDisplay.scaleFactor}x)`)
  },

  list(): DisplayInfo[] {
    return displays
  },

  byId(id: number): DisplayInfo | undefined {
    return displays.find(d => d.id === id)
  },

  primary(): DisplayInfo {
    return displays.find(d => d.primary) || displays[0]
  },

  sidebarTarget(): DisplayInfo {
    return targetDisplay || this.primary()
  },

  toPhysical(rectDip: Rect, sf: number): RectPx {
    return {
      x: Math.round(rectDip.x * sf),
      y: Math.round(rectDip.y * sf),
      width: Math.round(rectDip.width * sf),
      height: Math.round(rectDip.height * sf)
    }
  },

  toDIP(rectPx: RectPx, sf: number): Rect {
    return {
      x: Math.round(rectPx.x / sf),
      y: Math.round(rectPx.y / sf),
      width: Math.round(rectPx.width / sf),
      height: Math.round(rectPx.height / sf)
    }
  },

  withinWorkArea(display: DisplayInfo, rect: Rect): boolean {
    const wa = display.workArea
    return rect.x >= wa.x &&
           rect.y >= wa.y &&
           rect.x + rect.width <= wa.x + wa.width &&
           rect.y + rect.height <= wa.y + wa.height
  },

  windowOrigin(win: BrowserWindow): { x: number; y: number } {
    const bounds = win.getBounds()
    return { x: bounds.x, y: bounds.y }
  },

  // 计算侧边栏位置 (DIP)
  calculateSidebarBounds(config: SidekickConfig): Rect {
    const target = this.sidebarTarget()
    const side = config.display.sidebarSide
    const width = 52  // 收起态宽度
    const height = target.workArea.height

    let x: number
    if (side === 'right') {
      x = target.workArea.x + target.workArea.width - width
    } else {
      x = target.workArea.x
    }

    return {
      x,
      y: target.workArea.y,
      width,
      height
    }
  },

  // 计算展开面板位置
  calculatePanelBounds(config: SidekickConfig): Rect {
    const target = this.sidebarTarget()
    const side = config.display.sidebarSide
    const panelWidth = 380
    const railWidth = 52

    let x: number
    if (side === 'right') {
      x = target.workArea.x + target.workArea.width - railWidth - panelWidth
    } else {
      x = target.workArea.x + railWidth
    }

    return {
      x,
      y: target.workArea.y,
      width: panelWidth,
      height: target.workArea.height
    }
  }
}

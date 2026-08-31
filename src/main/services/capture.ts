// services/capture.ts - 截图服务 (desktopCapturer + 区域裁剪 + 批注支持)

import { desktopCapturer, app, clipboard } from 'electron'
import { writeFileSync, existsSync, mkdirSync, unlinkSync } from 'fs'
import { join } from 'path'
import log from 'electron-log'
import { ConfigService } from './config'
import { DisplayService } from './display'
import type { DisplayInfo, Rect } from '../../shared/types'

function pad(n: number): string {
  return String(n).padStart(2, '0')
}

function ensureDir(dir: string): void {
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true })
}

export const CaptureService = {
  // 获取屏幕源
  async getScreenSources() {
    return desktopCapturer.getSources({
      types: ['screen'],
      thumbnailSize: { width: 3840, height: 2160 },
      fetchWindowIcons: false
    })
  },

  // 核心截图: 直接通过 thumbnail 截全屏
  async grabFullscreen(): Promise<Electron.NativeImage | null> {
    try {
      const sources = await this.getScreenSources()
      if (!sources || sources.length === 0) {
        log.error('[Capture] No screen sources found')
        return null
      }
      // 取第一个屏幕
      const img = sources[0].thumbnail
      if (!img || img.isEmpty()) {
        log.error('[Capture] Thumbnail is empty')
        return null
      }
      const size = img.getSize()
      log.info(`[Capture] Grabbed ${size.width}x${size.height}`)
      return img
    } catch (e: any) {
      log.error('[Capture] grabFullscreen failed:', e.message)
      return null
    }
  },

  // 目标显示器截图 (多显示器场景): 截图源优先匹配侧边栏所在显示器,
  // 显示器热插拔后由 DisplayService 自动回退到有效显示器, 保证快捷键截图吸附到正确屏幕
  async grabTarget(): Promise<{ img: Electron.NativeImage | null; target: DisplayInfo; workArea: Rect; bounds: Rect } | null> {
    try {
      const sources = await this.getScreenSources()
      if (!sources || sources.length === 0) {
        log.error('[Capture] No screen sources found')
        return null
      }

      const target = DisplayService.sidebarTarget()
      // Electron desktopCapturer 的 display_id 为字符串, 与 screen display.id 同源编号
      let source = sources.find(s => s.display_id !== '' && String(s.display_id) === String(target.id))
      if (!source) {
        source = sources[0]
        log.info('[Capture] No exact display match, falling back to first source')
      }

      const img = source.thumbnail
      if (!img || img.isEmpty()) {
        log.error('[Capture] Thumbnail is empty')
        return null
      }

      // 源对应的显示器: 若确实映射到另一块屏则用它的 workArea, 保证裁剪坐标原点正确
      let matched = target
      if (String(source.display_id) !== String(target.id)) {
        const byId = DisplayService.byId(parseInt(source.display_id))
        if (byId) matched = byId
      }

      const size = img.getSize()
      log.info(`[Capture] Grabbed ${size.width}x${size.height} (source=${source.display_id}, target=${target.id})`)
      // bounds 一并返回: 源图像 (desktopCapturer thumbnail) 以该显示器 bounds 原点为 (0,0),
      // 副屏 (bounds.x/y ≠ 0) 裁剪坐标必须扣除 bounds 原点, 见 main.ts 区域换算 (P0-3)
      return { img, target: matched, workArea: matched.workArea, bounds: matched.bounds }
    } catch (e: any) {
      log.error('[Capture] grabTarget failed:', e.message)
      return null
    }
  },

  // 保存截图 (可选裁剪)
  async saveImage(
    img: Electron.NativeImage,
    bounds?: { x: number; y: number; width: number; height: number }
  ): Promise<string | null> {
    try {
      let finalImg = img
      if (bounds) {
        const size = img.getSize()
        const x = Math.max(0, Math.min(Math.round(bounds.x), size.width - 1))
        const y = Math.max(0, Math.min(Math.round(bounds.y), size.height - 1))
        const w = Math.min(Math.round(bounds.width), size.width - x)
        const h = Math.min(Math.round(bounds.height), size.height - y)
        if (w < 2 || h < 2) {
          log.warn('[Capture] Crop bounds too small:', { x, y, w, h })
          return null
        }
        finalImg = img.crop({ x, y, width: w, height: h })
        log.info(`[Capture] Cropped to ${w}x${h} at (${x},${y})`)
      }

      const cfg = ConfigService.get()
      const dir = cfg.capture.dir
      ensureDir(dir)

      const now = new Date()
      const timestamp = `${now.getFullYear()}${pad(now.getMonth()+1)}${pad(now.getDate())}_${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`
      const ext = cfg.capture.format.toLowerCase() === 'jpg' ? 'jpg' : 'png'
      const filename = `seewo-capture-${timestamp}.${ext}`
      const filepath = join(dir, filename)

      if (ext === 'jpg') {
        writeFileSync(filepath, finalImg.toJPEG(90))
      } else {
        writeFileSync(filepath, finalImg.toPNG())
      }

      // MLP 1.11-⑦: 结果入剪贴板
      clipboard.writeImage(finalImg)
      log.info(`[Capture] Copied to clipboard + saved: ${filepath}`)
      return filepath
    } catch (e: any) {
      log.error('[Capture] saveImage failed:', e.message)
      return null
    }
  },

  // 保存到临时文件 (用于批注背景)
  saveTempImage(img: Electron.NativeImage): string {
    const tmpDir = join(app.getPath('temp'), 'seewo-sidekick')
    ensureDir(tmpDir)
    const filepath = join(tmpDir, `screenshot-${Date.now()}.png`)
    writeFileSync(filepath, img.toPNG())
    log.info(`[Capture] Temp saved: ${filepath}`)
    return filepath
  },

  // 保存批注结果 (dataUrl -> 文件)
  saveAnnotatedImage(dataUrl: string): string | null {
    try {
      // data:image/png;base64,xxxx
      const base64 = dataUrl.split(',')[1]
      if (!base64) {
        log.error('[Capture] Invalid dataUrl')
        return null
      }
      const buffer = Buffer.from(base64, 'base64')

      const cfg = ConfigService.get()
      const dir = cfg.capture.dir
      ensureDir(dir)

      const now = new Date()
      const timestamp = `${now.getFullYear()}${pad(now.getMonth()+1)}${pad(now.getDate())}_${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`
      const filename = `seewo-annotate-${timestamp}.png`
      const filepath = join(dir, filename)

      writeFileSync(filepath, buffer)
      log.info(`[Capture] Annotated saved: ${filepath}`)
      return filepath
    } catch (e: any) {
      log.error('[Capture] saveAnnotatedImage failed:', e.message)
      return null
    }
  },

  // 清理临时文件
  cleanupTemp(filepath: string): void {
    try {
      if (existsSync(filepath)) {
        unlinkSync(filepath)
        log.info(`[Capture] Cleaned temp: ${filepath}`)
      }
    } catch {
      // 忽略清理失败
    }
  }
}

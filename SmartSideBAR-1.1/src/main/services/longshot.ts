// services/longshot.ts - 长截图服务 v3 (窗口选择 + 智能滚动拼接)

import { desktopCapturer, clipboard, nativeImage, Notification } from 'electron'
import { spawn, exec } from 'child_process'
import sharp from 'sharp'
import { writeFileSync, existsSync, mkdirSync } from 'fs'
import { join } from 'path'
import log from 'electron-log'
import { ConfigService } from './config'
import { WindowManager } from '../windows/manager'

let longshotRunning = false
let longshotStopRequested = false

function sleep(ms: number): Promise<void> {
  return new Promise(r => setTimeout(r, ms))
}

function pad(n: number): string {
  return String(n).padStart(2, '0')
}

function ensureDir(dir: string): void {
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true })
}

function timestamp(): string {
  const now = new Date()
  return `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}_${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`
}

/** 将 PowerShell 脚本编码为 Base64 (UTF-16LE) */
function toBase64Command(script: string): string {
  const buf = Buffer.from(script, 'utf16le')
  return buf.toString('base64')
}

function broadcastProgress(data: any) {
  WindowManager.broadcast('longshot:progress', data)
}

function broadcastCountdown(n: number) {
  WindowManager.broadcast('longshot:countdown', n)
}

export interface WindowInfo {
  pid: number
  name: string
  title: string
  handle: number
  x: number
  y: number
  width: number
  height: number
}

export const LongshotService = {
  isRunning(): boolean { return longshotRunning },

  /** 枚举可见窗口列表 */
  async listWindows(): Promise<{ success: boolean; windows: WindowInfo[]; error?: string }> {
    const PS_LIST = `Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class WinAPI {
  [DllImport(\"user32.dll\")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
  [DllImport(\"user32.dll\")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport(\"user32.dll\")] public static extern bool IsIconic(IntPtr hWnd);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@

$procs = Get-Process | Where-Object {
  \$_.MainWindowHandle -ne 0 -and
  -not [string]::IsNullOrWhiteSpace(\$_.MainWindowTitle) -and
  [WinAPI]::IsWindowVisible(\$_.MainWindowHandle) -and
  -not [WinAPI]::IsIconic(\$_.MainWindowHandle)
} | ForEach-Object {
  \$rect = New-Object WinAPI+RECT
  [WinAPI]::GetWindowRect(\$_.MainWindowHandle, [ref]\$rect) | Out-Null
  @{
    pid = \$_.Id
    name = \$_.ProcessName
    title = \$_.MainWindowTitle
    handle = [int]\$_.MainWindowHandle
    x = \$rect.Left
    y = \$rect.Top
    width = \$rect.Right - \$rect.Left
    height = \$rect.Bottom - \$rect.Top
  }
}
@(\$procs) | ConvertTo-Json -Depth 3
`
    return new Promise((resolve) => {
      const child = spawn('powershell', [
        '-NoProfile', '-NonInteractive', '-EncodedCommand', toBase64Command(PS_LIST)
      ], { timeout: 8000 })
      let out = ''
      let err = ''
      child.stdout.on('data', d => { out += d.toString() })
      child.stderr.on('data', d => { err += d.toString() })
      child.on('close', (code) => {
        if (code !== 0 || !out.trim()) {
          log.warn('[Longshot] listWindows failed:', err || `exit ${code}`)
          resolve({ success: false, windows: [], error: err || `PowerShell exit ${code}` })
          return
        }
        try {
          const parsed = JSON.parse(out)
          const arr = Array.isArray(parsed) ? parsed : [parsed]
          const windows: WindowInfo[] = arr
            .filter((w: any) => w.width > 200 && w.height > 200)
            .map((w: any) => ({
              pid: w.pid,
              name: w.name,
              title: w.title,
              handle: w.handle,
              x: w.x,
              y: w.y,
              width: w.width,
              height: w.height
            }))
          resolve({ success: true, windows })
        } catch (e: any) {
          log.warn('[Longshot] parse windows failed:', e.message)
          resolve({ success: false, windows: [], error: e.message })
        }
      })
    })
  },

  /** 启动长截图: opts = { window: WindowInfo } */
  async start(opts?: { window?: WindowInfo }): Promise<string | null> {
    if (longshotRunning) { log.warn('[Longshot] Already running'); return null }
    longshotRunning = true
    longshotStopRequested = false
    log.info('[Longshot] Starting... opts=', opts)

    const cfg = ConfigService.get()
    const dir = cfg.capture.dir
    ensureDir(dir)

    if (!opts?.window) {
      log.error('[Longshot] No window selected')
      longshotRunning = false
      return null
    }

    const win = opts.window

    // 1. 倒计时 (3..1)，让用户有时间切到目标窗口
    for (let i = 3; i >= 1; i--) {
      broadcastCountdown(i)
      await sleep(1000)
    }
    broadcastCountdown(0)

    // 2. 激活目标窗口
    await this._activateWindow(win)

    // 3. 隐藏侧边栏，让用户专注于目标窗口
    WindowManager.hideMain()
    await sleep(800)

    const frames: Buffer[] = []
    const maxFrames = 80
    const maxTotalHeight = 25000
    const betweenDelay = 1000 // 等待滚动动画稳定
    const stopDetectFrames = 3

    try {
      log.info(`[Longshot] Capturing window "${win.title}" at ${win.x},${win.y} ${win.width}x${win.height}`)

      // 首帧
      const first = await this._captureWindow(win)
      if (!first) throw new Error('无法截取窗口图像，请确认目标窗口未被最小化')
      frames.push(first.buf)
      broadcastProgress({ frameIndex: 0, totalHeight: first.info.height, status: 'capturing', title: win.title })

      let consecutiveSame = 0
      let lastBuf = first.buf

      for (let i = 1; i < maxFrames; i++) {
        if (longshotStopRequested) { log.info('[Longshot] Stop requested'); break }

        // 激活 + 发送 PageDown
        await this._scrollWindow(win)
        await sleep(betweenDelay)

        const captured = await this._captureWindow(win)
        if (!captured) { log.warn(`[Longshot] Frame ${i} capture failed, stopping`); break }

        // 智能检测是否到底：比较裁剪区域
        const { buf } = captured
        const similar = await this._framesSimilar(lastBuf, buf, win.width)
        if (similar) {
          consecutiveSame++
          log.info(`[Longshot] Frame ${i} similar (${consecutiveSame}/${stopDetectFrames})`)
          if (consecutiveSame >= stopDetectFrames) {
            log.info('[Longshot] Bottom detected')
            break
          }
        } else {
          consecutiveSame = 0
        }

        frames.push(buf)
        lastBuf = buf

        let totalHeight = 0
        for (const f of frames) {
          const m = await sharp(f).metadata()
          totalHeight += m.height || win.height
        }

        broadcastProgress({ frameIndex: i, totalHeight, status: 'capturing', title: win.title })
        if (totalHeight >= maxTotalHeight) { log.info('[Longshot] Max height reached'); break }
      }

      // 拼接
      broadcastProgress({ frameIndex: frames.length, totalHeight: 0, status: 'stitching', title: win.title })
      const stitched = await this._stitchFrames(frames, win.width)
      if (!stitched) throw new Error('拼接失败')

      const filename = `seewo-longshot-${timestamp()}.png`
      const filepath = join(dir, filename)
      writeFileSync(filepath, stitched)
      clipboard.writeImage(nativeImage.createFromBuffer(stitched))
      log.info(`[Longshot] Saved: ${filepath}`)

      broadcastProgress({ frameIndex: frames.length, totalHeight: 0, status: 'done', filepath, title: win.title })
      return filepath
    } catch (e: any) {
      log.error('[Longshot] Error:', e.message)
      broadcastProgress({ frameIndex: frames.length, totalHeight: 0, status: 'error', error: e.message })
      return null
    } finally {
      longshotRunning = false
      longshotStopRequested = false
      WindowManager.showMain()
    }
  },

  stop(): void {
    if (longshotRunning) { longshotStopRequested = true; log.info('[Longshot] Stop requested') }
  },

  /** 激活目标窗口并置前 */
  async _activateWindow(win: WindowInfo): Promise<void> {
    const PS_ACTIVATE = `$wshell = New-Object -ComObject WScript.Shell
[void]$wshell.AppActivate(${win.pid})
Start-Sleep -Milliseconds 200
`
    return new Promise(resolve => {
      exec(`powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_ACTIVATE)}"`, { timeout: 5000 }, () => resolve())
    })
  },

  /** 向目标窗口发送 PageDown */
  async _scrollWindow(win: WindowInfo): Promise<void> {
    const PS_SCROLL = `$wshell = New-Object -ComObject WScript.Shell
[void]$wshell.AppActivate(${win.pid})
Start-Sleep -Milliseconds 100
$wshell.SendKeys('{PGDN}')
`
    return new Promise(resolve => {
      exec(`powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_SCROLL)}"`, { timeout: 3000 }, () => resolve())
    })
  },

  /** 截取指定窗口区域 */
  async _captureWindow(win: WindowInfo): Promise<{ buf: Buffer; info: { width: number; height: number } } | null> {
    try {
      const sources = await desktopCapturer.getSources({
        types: ['screen'],
        thumbnailSize: { width: 3840, height: 2160 },
        fetchWindowIcons: false
      })
      if (!sources?.length) return null

      const img = sources[0].thumbnail
      if (!img || img.isEmpty()) return null

      const fullBuf = img.toPNG()

      // 获取截图实际尺寸
      const meta = await sharp(fullBuf).metadata()
      const maxW = meta.width || 3840
      const maxH = meta.height || 2160

      const cropX = Math.max(0, win.x)
      const cropY = Math.max(0, win.y)
      const cropW = Math.min(win.width, maxW - cropX)
      const cropH = Math.min(win.height, maxH - cropY)

      if (cropW <= 10 || cropH <= 10) return null

      const cropped = await sharp(fullBuf)
        .extract({ left: cropX, top: cropY, width: cropW, height: cropH })
        .png()
        .toBuffer()

      return { buf: cropped, info: { width: cropW, height: cropH } }
    } catch (e: any) {
      log.error('[Longshot] _captureWindow failed:', e.message)
      return null
    }
  },

  /** 帧相似度检测 (行级像素差异) */
  async _framesSimilar(bufA: Buffer, bufB: Buffer, width: number, threshold = 0.92): Promise<boolean> {
    try {
      const metaA = await sharp(bufA).metadata()
      const metaB = await sharp(bufB).metadata()
      if ((metaA.width || 0) !== (metaB.width || 0)) return false
      if (Math.abs((metaA.height || 0) - (metaB.height || 0)) > 5) return false

      // 转换为 raw RGBA
      const rawA = await sharp(bufA).raw().toBuffer({ resolveWithObject: true })
      const rawB = await sharp(bufB).raw().toBuffer({ resolveWithObject: true })
      const hA = rawA.info.height
      const hB = rawB.info.height
      const w = rawA.info.width
      const ch = rawA.info.channels
      const h = Math.min(hA, hB)

      let sameRows = 0
      for (let y = 0; y < h; y++) {
        const rowStart = y * w * ch
        let rowDiff = 0
        const step = Math.max(1, Math.floor(w / 40))
        let samples = 0
        for (let x = 0; x < w; x += step) {
          const i = rowStart + x * ch
          if (i + 2 >= rawA.data.length || i + 2 >= rawB.data.length) continue
          const diff = Math.abs(rawA.data[i] - rawB.data[i]) +
            Math.abs(rawA.data[i + 1] - rawB.data[i + 1]) +
            Math.abs(rawA.data[i + 2] - rawB.data[i + 2])
          if (diff > 30) rowDiff++
          samples++
        }
        if (rowDiff < samples * 0.1) sameRows++
      }

      return sameRows / h >= threshold
    } catch (e: any) {
      log.warn('[Longshot] framesSimilar error:', e.message)
      return false
    }
  },

  /** 智能拼接: 找最优重叠偏移，裁剪后合成 */
  async _stitchFrames(frames: Buffer[], expectedWidth: number): Promise<Buffer | null> {
    if (frames.length === 0) return null
    if (frames.length === 1) return frames[0]

    try {
      // 获取所有帧尺寸
      const metas = await Promise.all(frames.map(f => sharp(f).metadata()))
      const widths = metas.map(m => m.width || expectedWidth)
      const heights = metas.map(m => m.height || 1080)
      const width = Math.max(...widths)

      // 计算每帧之间的最优重叠
      const overlaps: number[] = [0]
      for (let i = 1; i < frames.length; i++) {
        const overlap = await this._findBestOverlap(frames[i - 1], frames[i], width)
        overlaps.push(overlap)
      }

      // 裁剪并收集
      const croppedBuffers: Buffer[] = []
      for (let i = 0; i < frames.length; i++) {
        const h = heights[i]
        const overlap = overlaps[i]
        if (i === 0) {
          croppedBuffers.push(frames[0])
        } else {
          const cropH = Math.max(1, h - overlap)
          if (cropH > 0) {
            const cropped = await sharp(frames[i])
              .extract({ left: 0, top: overlap, width: Math.min(width, widths[i]), height: cropH })
              .png()
              .toBuffer()
            croppedBuffers.push(cropped)
          }
        }
      }

      // 获取裁剪后尺寸
      const croppedMetas = await Promise.all(croppedBuffers.map(f => sharp(f).metadata()))
      const totalHeight = croppedMetas.reduce((sum, m) => sum + (m.height || 0), 0)

      // 创建画布并 composite
      const canvas = sharp({
        create: {
          width,
          height: totalHeight,
          channels: 4,
          background: { r: 255, g: 255, b: 255, alpha: 1 }
        }
      })

      let y = 0
      const composites: Array<{ input: Buffer; top: number; left: number }> = []
      for (let i = 0; i < croppedBuffers.length; i++) {
        const buf = croppedBuffers[i]
        const m = croppedMetas[i]
        composites.push({ input: buf, top: y, left: 0 })
        y += (m.height || 0)
      }

      const result = await canvas.composite(composites).png().toBuffer()
      return result
    } catch (e: any) {
      log.error('[Longshot] Smart stitch failed:', e.message)
      return this._simpleStitch(frames, expectedWidth)
    }
  },

  /** 找两帧之间的最佳重叠高度 */
  async _findBestOverlap(bufPrev: Buffer, bufCurr: Buffer, width: number): Promise<number> {
    try {
      const rawPrev = await sharp(bufPrev).raw().toBuffer({ resolveWithObject: true })
      const rawCurr = await sharp(bufCurr).raw().toBuffer({ resolveWithObject: true })
      const hPrev = rawPrev.info.height
      const hCurr = rawCurr.info.height
      const w = Math.min(rawPrev.info.width, rawCurr.info.width, width)
      const ch = rawPrev.info.channels

      // 取前一帧底部 30% 和当前帧顶部做比较
      const searchH = Math.min(hPrev, hCurr, Math.floor(hPrev * 0.3))
      if (searchH < 10) return 0

      let bestOverlap = 0
      let bestScore = Infinity

      for (let offset = 0; offset < searchH; offset++) {
        const prevRow = hPrev - searchH + offset
        if (prevRow < 0) continue

        let diff = 0
        const step = Math.max(1, Math.floor(w / 30))
        let samples = 0

        const compareRows = Math.min(20, hCurr)
        for (let r = 0; r < compareRows; r++) {
          const currRow = r
          const prevIdx = (prevRow + r) * w * ch
          const currIdx = currRow * w * ch
          if (prevIdx + 2 >= rawPrev.data.length || currIdx + 2 >= rawCurr.data.length) continue

          for (let x = 0; x < w; x += step) {
            const pi = prevIdx + x * ch
            const ci = currIdx + x * ch
            if (pi + 2 >= rawPrev.data.length || ci + 2 >= rawCurr.data.length) continue
            diff += Math.abs(rawPrev.data[pi] - rawCurr.data[ci]) +
              Math.abs(rawPrev.data[pi + 1] - rawCurr.data[ci + 1]) +
              Math.abs(rawPrev.data[pi + 2] - rawCurr.data[ci + 2])
            samples++
          }
        }

        if (samples > 0) {
          const score = diff / samples
          if (score < bestScore) {
            bestScore = score
            bestOverlap = offset
          }
        }
      }

      // 阈值判断: 如果 bestScore 太大，说明没找到好的匹配，返回保守值
      if (bestScore > 80) {
        return Math.floor(searchH / 3)
      }

      return bestOverlap
    } catch (e: any) {
      log.warn('[Longshot] findBestOverlap failed:', e.message)
      return 80
    }
  },

  /** 降级简单拼接 */
  async _simpleStitch(frames: Buffer[], expectedWidth: number): Promise<Buffer | null> {
    try {
      const metas = await Promise.all(frames.map(f => sharp(f).metadata()))
      const width = Math.max(...metas.map(m => m.width || expectedWidth))
      const totalHeight = metas.reduce((sum, m) => sum + (m.height || 0), 0)

      const canvas = sharp({
        create: { width, height: totalHeight, channels: 4, background: { r: 255, g: 255, b: 255, alpha: 1 } }
      })

      let y = 0
      const composites: Array<{ input: Buffer; top: number; left: number }> = []
      for (let i = 0; i < frames.length; i++) {
        composites.push({ input: frames[i], top: y, left: 0 })
        y += (metas[i].height || 0)
      }

      return await canvas.composite(composites).png().toBuffer()
    } catch (e: any) {
      log.error('[Longshot] Simple stitch failed:', e.message)
      return null
    }
  }
}

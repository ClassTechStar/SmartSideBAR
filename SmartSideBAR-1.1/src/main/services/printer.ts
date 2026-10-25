// services/printer.ts - 打印机监控服务

import { exec } from 'child_process'
import { promisify } from 'util'
import log from 'electron-log'
import { BrowserWindow } from 'electron'
import type { PrinterStatus, SidekickConfig } from '../../shared/types'
import { IPC_CHANNELS } from '../../shared/ipc-channels'

const execAsync = promisify(exec)

let polling: NodeJS.Timeout | null = null
let lastStatus: Map<string, PrinterStatus> = new Map()

// PowerShell 获取打印机状态
const PS_GET_PRINTERS = `
Get-CimInstance Win32_Printer | Select-Object Name,PrinterState,PrinterStatus,DetectedErrorState | ConvertTo-Json
`

function mapState(state: number): PrinterStatus['state'] {
  // Win32_Printer status mapping
  if (state === 0) return 'unknown'
  if (state & 0x80) return 'out_of_paper'
  if (state & 0x40) return 'jammed'
  if (state & 0x200000) return 'low_ink'
  if (state === 3) return 'ok'
  if (state === 7) return 'offline'
  return 'unknown'
}

export const PrinterService = {
  async start(cfg: SidekickConfig): Promise<void> {
    const interval = Math.max(cfg.printer.pollIntervalSec, 5) * 1000

    // 初始扫描
    await this.refresh()

    // 轮询
    polling = setInterval(async () => {
      await this.refresh()
    }, interval)

    log.info(`[Printer] Service started (poll=${interval}ms)`)
  },

  stop(): void {
    if (polling) {
      clearInterval(polling)
      polling = null
    }
    log.info('[Printer] Service stopped')
  },

  async refresh(): Promise<void> {
    try {
      const { stdout } = await execAsync(`powershell -NoProfile -Command "${PS_GET_PRINTERS}"`, { timeout: 8000 })
      let printers: any[] = []
      try {
        const data = JSON.parse(stdout.trim())
        printers = Array.isArray(data) ? data : [data]
      } catch { /* 空结果 */ }

      const current = new Map<string, PrinterStatus>()
      for (const p of printers) {
        const status: PrinterStatus = {
          name: p.Name,
          state: mapState(p.PrinterState || p.PrinterStatus || 0),
          updatedAt: Date.now()
        }
        current.set(p.Name, status)

        // 状态变化时广播
        const prev = lastStatus.get(p.Name)
        if (!prev || prev.state !== status.state) {
          if (status.state !== 'ok' && status.state !== 'unknown') {
            this.broadcast(status)
          }
        }
      }
      lastStatus = current
    } catch (e) {
      log.warn('[Printer] Refresh failed:', e)
    }
  },

  async getStatus(): Promise<PrinterStatus[]> {
    return Array.from(lastStatus.values())
  },

  broadcast(status: PrinterStatus): void {
    for (const win of BrowserWindow.getAllWindows()) {
      win.webContents.send(IPC_CHANNELS['printer:changed'], status)
    }
  }
}

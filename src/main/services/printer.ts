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
// E4 修复: 已选 DetectedErrorState 但原 mapState 未使用它, 位掩码用错属性
const PS_GET_PRINTERS = `
Get-CimInstance Win32_Printer | Select-Object Name,PrinterState,PrinterStatus,DetectedErrorState | ConvertTo-Json
`

// E4 修复: Win32_Printer 的 DetectedErrorState 是枚举值 (非位掩码),
// 原实现用位运算 (& 0x80/0x40/0x200000) 在 PrinterState 上, 判定全部错误。
// PrinterState===3 被判为 ok, 实际 3 = Pending Deletion / 纸张问题。
// 改为: DetectedErrorState 枚举优先 (精确错误), PrinterStatus 枚举补充 (整体状态)。
//
// DetectedErrorState (WMI 枚举):
//   0=Unknown 1=Other 2=No Error 3=Low Paper 4=No Paper
//   5=Low Toner/Ink 6=No Toner/Ink 7=Door Open 8=Paper Jam
//   9=Offline 10=Service Requested 11=Output Bin Full
//
// PrinterStatus (WMI 枚举, 替代已废弃的 PrinterState):
//   1=Other 2=Unknown 3=Idle 4=Printing 5=Warmup 6=Stopped 7=Offline

function mapState(detectedError: number, printerStatus: number): PrinterStatus['state'] {
  // DetectedErrorState 枚举优先检测具体错误
  if (detectedError === 4 || detectedError === 3) return 'out_of_paper'  // No Paper / Low Paper
  if (detectedError === 5 || detectedError === 6) return 'low_ink'       // Low/No Toner/Ink
  if (detectedError === 8) return 'jammed'                               // Paper Jam
  if (detectedError === 9) return 'offline'                                // Offline
  if (detectedError === 10) return 'unknown'                              // Service Requested

  // PrinterStatus 枚举补充 (无具体错误时判定整体)
  if (printerStatus === 7) return 'offline'
  if (printerStatus === 3 || printerStatus === 4 || printerStatus === 5) return 'ok'  // Idle/Printing/Warmup

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
          state: mapState(p.DetectedErrorState || 0, p.PrinterStatus || p.PrinterState || 0),
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
    // E6 修复: 加 isDestroyed() 保护 (与 UsbService 一致)
    for (const win of BrowserWindow.getAllWindows()) {
      if (!win.isDestroyed()) {
        win.webContents.send(IPC_CHANNELS['printer:changed'], status)
      }
    }
  }
}

// services/usb.ts - USB 监控服务 (v5.1 PS5.1兼容版)
// 检测策略: Windows WMI 事件驱动 (Win32_VolumeChangeEvent) + 低频后备轮询
// PowerShell 5.1 兼容: 不使用 -AsArray 参数, 改用 @() 数组强制包装
//
// MLP验收(1.11-3): 事件驱动零延迟 + 提示音 + 通知 + 不抢焦点

import { exec, spawn, type ChildProcess } from 'child_process'
import { promisify } from 'util'
import log from 'electron-log'
import { BrowserWindow, Notification } from 'electron'
import type { DriveEvent, SidekickConfig } from '../../shared/types'
import { IPC_CHANNELS } from '../../shared/ipc-channels'

const execAsync = promisify(exec)

let psWatcher: ChildProcess | null = null
let fallbackPolling: NodeJS.Timeout | null = null
let lastDrives: Map<string, { drive: string; label?: string; type: string; size?: string }> = new Map()
let isRunning = false
let isScanning = false
let pendingArrivalTimer: NodeJS.Timeout | null = null

const DEBOUNCE_MS = 3_000
const FALLBACK_POLL_MS = 8_000
const notifyHistory: Map<string, number> = new Map()

// === PowerShell 7.6.5 常驻脚本: 监听 Win32_VolumeChangeEvent ===
// EventType: 2 = Device arrival, 3 = Device removal
// PS 7.x 兼容: 添加 $ProgressPreference 消除 CLIXML 噪声
const PS_EVENT_WATCHER = `
$ProgressPreference = 'SilentlyContinue'
try {
    $watcher = New-Object System.Management.ManagementEventWatcher
    $query = New-Object System.Management.WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent")
    $watcher.Query = $query
    while ($true) {
        $event = $watcher.WaitForNextEvent()
        $drive = if ($event.DriveName) { $event.DriveName } else { "unknown" }
        $type = [string]$event.EventType
        Write-Host "USB_EVENT|$type|$drive" -NoNewline
        Write-Host ""  # 换行
    }
} catch {
    Write-Error "WMI_ERROR|$($_.Exception.Message)"
    exit 1
}
`

// === PowerShell 7.6.5 兼容: 综合检测脚本 v6.1 ===
// @(...) 强制数组包装, 确保 ConvertTo-Json 输出为 JSON 数组
// 修复: USB 3.0 SATA 桥接移动硬盘 InterfaceType='SCSI', 改为用 PNPDeviceID 检测
// 修复: PS 7.x CLIXML 噪声干扰, 添加 $ProgressPreference
// 修复: 部分 USB 3.0 移动硬盘 DriveType=3(Fixed), 增加 Size 阈值过滤排除系统盘
const PS_COMPREHENSIVE_SCRIPT = `
$ProgressPreference = 'SilentlyContinue'
$results = @()

# Part A: DriveType=2(Removable)/5(CD-ROM) direct detection
$logicalDisks = Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 2 -or $_.DriveType -eq 5 }
foreach ($ld in $logicalDisks) {
    $results += [PSCustomObject]@{
        DeviceID = $ld.DeviceID
        VolumeName = $ld.VolumeName
        DriveType = $ld.DriveType
        Size = $ld.Size
    }
}

# Part B: Trace USB-connected external/removable physical disks via CIM association
# Covers: USB flash drives, USB HDDs/SSDs (包括 USB 3.0 SATA 桥接芯片)
# 检测条件: InterfaceType='USB' OR PNPDeviceID 包含 'USB'/'USBSTOR' OR MediaType=External/Removable
$usbPhysDisks = Get-CimInstance Win32_DiskDrive | Where-Object {
    $_.InterfaceType -eq 'USB' -or
    $_.PNPDeviceID -like '*USB*' -or
    $_.PNPDeviceID -like '*USBSTOR*' -or
    $_.MediaType -eq 'Removable media' -or
    $_.MediaType -eq 'External hard disk media'
}

foreach ($disk in $usbPhysDisks) {
    # 使用 Get-CimAssociatedInstance 做 WMI 关联查询 (PS 3.0+ / PS 7.x 均支持)
    $partitions = $disk | Get-CimAssociatedInstance -ResultClassName Win32_DiskDriveToDiskPartition
    foreach ($partition in $partitions) {
        $logicalDisks = $partition | Get-CimAssociatedInstance -ResultClassName Win32_LogicalDiskToPartition
        foreach ($ld in $logicalDisks) {
            $exists = $false
            foreach ($r in $results) {
                if ($r.DeviceID -eq $ld.DeviceID) { $exists = $true; break }
            }
            if (-not $exists) {
                $results += [PSCustomObject]@{
                    DeviceID = $ld.DeviceID
                    VolumeName = $ld.VolumeName
                    DriveType = $ld.DriveType
                    Size = $ld.Size
                }
            }
        }
    }
}

# PS 7.x 兼容: @(...) 强制数组包装, 空数组输出 '[]'
@( $results ) | ConvertTo-Json -Depth 3
`

// === PowerShell 7.6.5 兼容: DriveType 降级脚本 ===
// 修复: PS 7.x CLIXML 噪声 + wmic 已移除, 纯用 Get-CimInstance
const PS_DRIVETYPE_SCRIPT = `
$ProgressPreference = 'SilentlyContinue'
$disks = Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 2 -or $_.DriveType -eq 5 } | Select-Object DeviceID, VolumeName, DriveType, Size
@( $disks ) | ConvertTo-Json -Depth 3
`

// 双音提示音
const PS_PLAY_SOUND = `
Add-Type -AssemblyName System.Windows.Forms
[System.Media.SystemSounds]::Asterisk.Play()
Start-Sleep -Milliseconds 240
[System.Media.SystemSounds]::Asterisk.Play()
`

/** UTF-16 LE Base64 编码 */
function toBase64Command(script: string): string {
  const buf = Buffer.from(script, 'utf16le')
  return buf.toString('base64')
}

/** 解析 PowerShell 5.1 JSON 输出 */
function parseWmiOutput(stdout: string): Array<{ drive: string; label?: string; type: string; size?: string }> {
  try {
    const text = stdout.trim()
    if (!text || text === '' || text === 'null') return []

    let data: any
    try {
      data = JSON.parse(text)
    } catch {
      // 尝试提取 JSON 片段
      const jsonMatch = text.match(/\[[\s\S]*\]/) || text.match(/\{[\s\S]*\}/)
      if (jsonMatch) data = JSON.parse(jsonMatch[0])
      else return []
    }

    // PS 5.1 @() 包装可能产生对象数组, 也可能在单条时产生对象而非数组
    const arr = Array.isArray(data) ? data : (data ? [data] : [])
    return arr
      .filter((d: any) => d && d.DeviceID)
      .map((d: any) => ({
        drive: d.DeviceID,
        label: d.VolumeName || undefined,
        type: d.DriveType === 2 ? 'removable' : (d.DriveType === 5 ? 'cdrom' : 'usb'),
        size: d.Size ? formatBytes(Number(d.Size)) : undefined
      }))
  } catch (e) {
    log.error('[USB] Parse error:', e, 'Raw:', stdout.slice(0, 300))
    return []
  }
}

function formatBytes(bytes: number): string {
  if (!bytes || bytes <= 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}

function playUsbSound(): void {
  exec(`powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_PLAY_SOUND)}"`,
    { timeout: 5000, windowsHide: true },
    (err) => { if (err) log.warn('[USB] Sound playback failed:', err.message) }
  )
}

function showUsbNotification(drive: string, label: string | undefined, size: string | undefined): void {
  if (!Notification.isSupported()) return
  const displayName = label || 'USB 存储设备'
  const sizeInfo = size ? ` (${size})` : ''
  const notif = new Notification({
    title: 'USB 设备接入',
    body: `${drive} ${displayName}${sizeInfo}`,
    timeoutType: 'default',
    silent: true
  })
  notif.on('click', () => {
    exec(`explorer "${drive}\\"`, { windowsHide: true })
  })
  notif.show()
}

/** 完整扫描 */
async function doScan(): Promise<Array<{ drive: string; label?: string; type: string; size?: string }>> {
  // 第 1 层: 综合检测
  try {
    const { stdout, stderr } = await execAsync(
      `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_COMPREHENSIVE_SCRIPT)}"`,
      { timeout: 10000, windowsHide: true, maxBuffer: 2 * 1024 * 1024 }
    )
    if (stderr?.trim()) log.warn('[USB] PS stderr:', stderr.trim().slice(0, 200))
    const results = parseWmiOutput(stdout)
    if (results.length > 0) return results
  } catch (e: any) { log.error('[USB] Comprehensive scan failed:', e.message) }

  // 第 2 层: DriveType 降级
  try {
    const { stdout } = await execAsync(
      `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_DRIVETYPE_SCRIPT)}"`,
      { timeout: 8000, windowsHide: true, maxBuffer: 1024 * 1024 }
    )
    const results = parseWmiOutput(stdout)
    if (results.length > 0) {
      log.info(`[USB] Fallback DriveType: ${results.length} device(s)`)
      return results
    }
  } catch (e: any) { log.error('[USB] DriveType fallback failed:', e.message) }

  // 第 3 层: WMIC
  try {
    const { stdout } = await execAsync(
      'wmic logicaldisk where "DriveType=2 or DriveType=5" get DeviceID,VolumeName,Size /format:csv',
      { timeout: 5000, windowsHide: true }
    )
    const results = parseWmicOutput(stdout)
    if (results.length > 0) {
      log.info(`[USB] Fallback WMIC: ${results.length} device(s)`)
      return results
    }
  } catch (e: any) { log.error('[USB] WMIC fallback failed:', e.message) }

  return []
}

/** 启动 WMI 事件监听器 */
function startWmiWatcher(): void {
  if (psWatcher) { psWatcher.kill(); psWatcher = null }

  log.info('[USB] Starting WMI event watcher (PS5.1 compatible)...')

  psWatcher = spawn('powershell', [
    '-NoProfile', '-NonInteractive', '-EncodedCommand', toBase64Command(PS_EVENT_WATCHER)
  ], {
    windowsHide: true,
    stdio: ['ignore', 'pipe', 'pipe']
  })

  let buffer = ''

  psWatcher.stdout?.on('data', (data: Buffer) => {
    buffer += data.toString()
    const lines = buffer.split('\n')
    buffer = lines.pop() || ''

    for (const line of lines) {
      const trimmed = line.trim()
      if (!trimmed) continue

      if (trimmed.startsWith('USB_EVENT')) {
        const parts = trimmed.split('|')
        const eventType = parts[1]
        const driveHint = parts[2] || 'unknown'
        log.info(`[USB] WMI event: type=${eventType}, drive=${driveHint}`)

        if (eventType === '2') {
          handleUsbArrival(driveHint)
        } else if (eventType === '3') {
          handleUsbRemoval(driveHint)
        }
      } else if (trimmed.includes('WMI_ERROR')) {
        log.error('[USB] WMI watcher error:', trimmed)
      }
    }
  })

  psWatcher.stderr?.on('data', (data: Buffer) => {
    log.warn('[USB] WMI watcher stderr:', data.toString().trim())
  })

  psWatcher.on('exit', (code) => {
    log.warn(`[USB] WMI watcher exited (code=${code}), restarting in 5s...`)
    psWatcher = null
    if (isRunning) {
      setTimeout(() => startWmiWatcher(), 5000)
    }
  })
}

function stopWmiWatcher(): void {
  if (psWatcher) {
    try { psWatcher.kill('SIGTERM') } catch (e) { /* ignore */ }
    psWatcher = null
  }
  if (pendingArrivalTimer) {
    clearTimeout(pendingArrivalTimer)
    pendingArrivalTimer = null
  }
}

/** 插入事件处理 */
async function handleUsbArrival(driveHint: string): Promise<void> {
  if (pendingArrivalTimer) clearTimeout(pendingArrivalTimer)
  pendingArrivalTimer = setTimeout(async () => {
    pendingArrivalTimer = null
    if (isScanning) return
    isScanning = true

    try {
      const results = await doScan()
      const currentMap = new Map<string, typeof results[0]>()
      for (const d of results) currentMap.set(d.drive, d)

      for (const d of results) {
        if (!lastDrives.has(d.drive)) {
          log.info(`[USB] >>> ARRIVED: ${d.drive} (${d.label || 'unknown'}, ${d.size || '?'})`)
          const now = Date.now()
          const lastNotify = notifyHistory.get(d.drive)
          if (lastNotify && (now - lastNotify) < DEBOUNCE_MS) {
            log.info(`[USB] Debounced: ${d.drive}`)
            continue
          }
          notifyHistory.set(d.drive, now)

          const event: DriveEvent = {
            drive: d.drive,
            label: d.label,
            type: d.type,
            size: d.size,
            at: now
          }
          UsbService.broadcast(IPC_CHANNELS['usb:arrived'], event)
          showUsbNotification(d.drive, d.label, d.size)
          playUsbSound()
        }
      }

      for (const [drive, info] of lastDrives) {
        if (!currentMap.has(drive)) {
          log.info(`[USB] <<< REMOVED: ${drive}`)
          notifyHistory.delete(drive)
          const event: DriveEvent = { drive, label: info.label, type: info.type, at: Date.now() }
          UsbService.broadcast(IPC_CHANNELS['usb:removed'], event)
        }
      }

      lastDrives = currentMap
    } catch (e: any) {
      log.error('[USB] Arrival handler error:', e.message)
    } finally {
      isScanning = false
    }
  }, 300)
}

/** 移除事件处理 */
async function handleUsbRemoval(driveHint: string): Promise<void> {
  setTimeout(async () => {
    if (isScanning) return
    isScanning = true

    try {
      const results = await doScan()
      const currentMap = new Map<string, typeof results[0]>()
      for (const d of results) currentMap.set(d.drive, d)

      for (const [drive, info] of lastDrives) {
        if (!currentMap.has(drive)) {
          log.info(`[USB] <<< REMOVED: ${drive}`)
          notifyHistory.delete(drive)
          const event: DriveEvent = { drive, label: info.label, type: info.type, at: Date.now() }
          UsbService.broadcast(IPC_CHANNELS['usb:removed'], event)
        }
      }

      lastDrives = currentMap
    } catch (e: any) {
      log.error('[USB] Removal handler error:', e.message)
    } finally {
      isScanning = false
    }
  }, 500)
}

/** 后备轮询 */
async function doFallbackPoll(): Promise<void> {
  if (isScanning) return
  isScanning = true

  try {
    const results = await doScan()
    const currentMap = new Map<string, typeof results[0]>()
    for (const d of results) currentMap.set(d.drive, d)

    for (const d of results) {
      if (!lastDrives.has(d.drive)) {
        log.info(`[USB] >>> ARRIVED (poll): ${d.drive}`)
        const now = Date.now()
        const lastNotify = notifyHistory.get(d.drive)
        if (!lastNotify || (now - lastNotify) >= DEBOUNCE_MS) {
          notifyHistory.set(d.drive, now)
          const event: DriveEvent = {
            drive: d.drive,
            label: d.label,
            type: d.type,
            size: d.size,
            at: now
          }
          UsbService.broadcast(IPC_CHANNELS['usb:arrived'], event)
          showUsbNotification(d.drive, d.label, d.size)
          playUsbSound()
        }
      }
    }

    for (const [drive, info] of lastDrives) {
      if (!currentMap.has(drive)) {
        log.info(`[USB] <<< REMOVED (poll): ${drive}`)
        notifyHistory.delete(drive)
        const event: DriveEvent = { drive, label: info.label, type: info.type, at: Date.now() }
        UsbService.broadcast(IPC_CHANNELS['usb:removed'], event)
      }
    }

    lastDrives = currentMap
  } catch (e: any) {
    log.warn('[USB] Fallback poll error:', e.message)
  } finally {
    isScanning = false
  }
}

export const UsbService = {
  async start(cfg: SidekickConfig): Promise<void> {
    if (isRunning) return
    if (!cfg.usb.enabled) {
      log.info('[USB] Disabled by config')
      return
    }

    isRunning = true

    // 初始扫描
    try {
      const initial = await doScan()
      lastDrives = new Map()
      for (const d of initial) lastDrives.set(d.drive, d)
      log.info(`[USB] Initial scan: ${initial.length} device(s)`)
    } catch (e: any) {
      log.error('[USB] Initial scan failed:', e.message)
    }

    // 主路径: WMI 事件驱动
    startWmiWatcher()

    // 后备路径: 低频轮询
    fallbackPolling = setInterval(() => doFallbackPoll(), FALLBACK_POLL_MS)

    log.info(`[USB] Service started v6 (PS7+ compatible, PNPDeviceID detection, event-driven + fallback ${FALLBACK_POLL_MS}ms)`)
  },

  stop(): void {
    isRunning = false
    stopWmiWatcher()
    if (fallbackPolling) {
      clearInterval(fallbackPolling)
      fallbackPolling = null
    }
    lastDrives.clear()
    notifyHistory.clear()
    log.info('[USB] Service stopped')
  },

  async scan(): Promise<Array<{ drive: string; label?: string; type: string; size?: string }>> {
    isScanning = true
    try {
      const results = await doScan()
      lastDrives = new Map()
      for (const d of results) lastDrives.set(d.drive, d)
      return results
    } finally {
      isScanning = false
    }
  },

  list(): Array<{ drive: string; label?: string; type: string; size?: string }> {
    return Array.from(lastDrives.values())
  },

  broadcast(channel: string, data: DriveEvent): void {
    for (const win of BrowserWindow.getAllWindows()) {
      if (!win.isDestroyed() && win.webContents && !win.webContents.isDestroyed()) {
        win.webContents.send(channel, data)
      }
    }
  },

  getCurrentDrives(): Map<string, { drive: string; label?: string; type: string; size?: string }> {
    return new Map(lastDrives)
  },

  getDiagnostics(): object {
    return {
      isRunning,
      isScanning,
      watcherAlive: psWatcher !== null && !psWatcher.killed,
      lastDrivesCount: lastDrives.size,
      drives: Array.from(lastDrives.values()),
      notifyHistoryCount: notifyHistory.size,
      fallbackPollMs: FALLBACK_POLL_MS,
      debounceMs: DEBOUNCE_MS,
      version: 'v6-ps7-compatible'
    }
  }
}

// WMIC CSV 解析降级
function parseWmicOutput(stdout: string): Array<{ drive: string; label?: string; type: string; size?: string }> {
  const lines = stdout.trim().split('\n').filter(l => l.trim())
  const results: Array<{ drive: string; label?: string; type: string; size?: string }> = []
  for (const line of lines) {
    if (line.includes('Node,')) continue
    const parts = line.split(',').map(p => p.trim().replace(/"/g, ''))
    if (parts.length >= 3) {
      const driveIdx = parts.findIndex(p => p.match(/^[A-Z]:$/))
      if (driveIdx >= 0) {
        const drive = parts[driveIdx]
        const label = parts[driveIdx + 1] || ''
        const size = parts[driveIdx + 2] || ''
        results.push({
          drive,
          label: label && label !== '' ? label : undefined,
          type: 'removable',
          size: size && !isNaN(Number(size)) ? formatBytes(Number(size)) : undefined
        })
      }
    }
  }
  return results
}

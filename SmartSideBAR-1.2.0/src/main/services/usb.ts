// services/usb.ts - USB 监控服务 v7
// 触发: Windows WMI 事件驱动 (Win32_VolumeChangeEvent) + 低频后备轮询
// 识别: 单次 PowerShell 调用内三层降级 (DriveType 2/5 → Get-Disk BusType → WQL ASSOCIATORS OF)
//
// 说明: 实际调用的是 Windows PowerShell 5.1 (spawn 'powershell'), 注释中的 "PowerShell 7.x"
//       指脚本语法对 5.1/7.x 均兼容, 而非运行时版本。
//
// MLP验收(1.11-3): 事件驱动零延迟 + 提示音 + 通知 + 不抢焦点

import { exec, spawn, type ChildProcess } from 'child_process'
import { promisify } from 'util'
import log from 'electron-log'
import { BrowserWindow, Notification } from 'electron'
import type { DriveEvent, SidekickConfig } from '../../shared/types'
import { IPC_CHANNELS } from '../../shared/ipc-channels'

const execAsync = promisify(exec)

/** 扫描结果模型: type 取值 removable(优盘) / external(移动硬盘) / cdrom(光驱) / usb(未分类) */
export interface UsbDrive {
  drive: string
  label?: string
  type: string
  size?: string
  model?: string
}

let psWatcher: ChildProcess | null = null
let lastDrives: Map<string, UsbDrive> = new Map()
let isRunning = false

// 扫描互斥 + 合并: 扫描进行中到达的事件不再被丢弃, 而是置 rescanQueued,
// 由 runScan() 在收尾后补跑一次。原实现 `if (isScanning) return` 会静默丢弃
// 「最后一个事件触发的扫描」, 而此刻后台跑的可能是事件之前启动的旧扫描,
// 结果就是状态停在陈旧数据上, 且要等下一次轮询才能自愈。
let isScanning = false
let rescanQueued = false
let pendingScanTimer: NodeJS.Timeout | null = null

// WMI 监听器健康度: 连续异常退出次数, 用于指数退避重启 (P1-3 落地)
let watcherFailures = 0
let watcherRestartTimer: NodeJS.Timeout | null = null
let watcherStartedAt = 0
// 轮询状态 (仅诊断用途): 当前生效的轮询间隔, 0 = 无轮询
let currentPollMs = 0
// 双定时器: 监听器健康时的低频巡检 + 监听器死亡时的高频降级兜底
let healthPollTimer: NodeJS.Timeout | null = null
let degradedPollTimer: NodeJS.Timeout | null = null
// D15: config.usb.ignoreTypes 过滤名单 (如 ['phone','carplay']), start() 时从配置读取
let ignoreTypes: string[] = []

const DEBOUNCE_MS = 3_000
// 后备轮询间隔 (P1-3):
//  - 监听器健康: 仅每 5 分钟一次巡检扫描 —— 事件驱动可信时不轮询,
//    巡检只为兜底「WMI 事件子系统静默失效」; 空闲 10 分钟约 2 次扫描 (验收 ≤3)
//  - 监听器异常退出: 临时回落 8s 高频, 保证重启完成前设备仍能被识别
const FALLBACK_POLL_MS = 300_000
const FALLBACK_POLL_MS_DEGRADED = 8_000
// 监听器重启退避: 5s → 10s → 20s → 40s → 60s 封顶
const WATCHER_BACKOFF_MS = 5_000
const WATCHER_BACKOFF_MAX_MS = 60_000
// WMI 事件合并窗口 (插入密集时一次硬件插入可能触发多个 VolumeChangeEvent)
const ARRIVAL_DEBOUNCE_MS = 300
const REMOVAL_DELAY_MS = 500
// notifyHistory 容量上限: 设备的移除事件若未被捕获会留下永久条目, 需定期清理
const NOTIFY_HISTORY_MAX = 64
const notifyHistory: Map<string, number> = new Map()

// === 常驻脚本: 监听 Win32_VolumeChangeEvent ===
// EventType: 2 = Device arrival, 3 = Device removal
// $ProgressPreference 用于消除进度条输出对解析的干扰
// 用 Write-Output 而非 Write-Host: Write-Host 写宿主而非成功输出流, 在进程被重定向时
// 是否被捕获取决于宿主实现; Write-Output 语义明确。随后调用 Out.Flush() 强制冲刷,
// 避免 PowerShell 缓冲导致事件迟迟不到达 Node 侧。
const PS_EVENT_WATCHER = `
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
try {
    $watcher = New-Object System.Management.ManagementEventWatcher
    $query = New-Object System.Management.WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent")
    $watcher.Query = $query
    while ($true) {
        $event = $watcher.WaitForNextEvent()
        $drive = if ($event.DriveName) { $event.DriveName } else { "unknown" }
        $type = [string]$event.EventType
        Write-Output ("USB_EVENT|" + $type + "|" + $drive)
        [Console]::Out.Flush()
    }
} catch {
    Write-Error ("WMI_ERROR|" + $_.Exception.Message)
    exit 1
}
`

// === 统一检测脚本 v7 ===
// 三层合一, 单次 PowerShell 调用完成: DriveType 2/5 → Get-Disk(BusType) → WQL ASSOCIATORS OF
//
// 实测依据 (Windows 11 + JMicron USB3.0 桥接移动硬盘, 2026-08-30 本机验证):
//  1. USB 移动硬盘在 Windows 上 DriveType=3(本地磁盘), 与内置盘完全相同 → 无法用 DriveType 区分
//  2. USB 桥接芯片把 InterfaceType 伪装成 'SCSI', PNPDeviceID 形如 'SCSI\DISK&VEN_JMICRON...'
//     → InterfaceType='USB' 与 PNPDeviceID -like '*USB*' / '*USBSTOR*' 全部不命中
//  3. Get-CimAssociatedInstance -ResultClassName Win32_DiskDriveToDiskPartition 实测返回 0 条
//     (同一对象不带 -ResultClassName 时可返回 4 个关联类, 说明 -ResultClassName 匹配失败)
//     → 改用经典 WQL 'ASSOCIATORS OF' 查询, 实测稳定返回分区与逻辑盘
//  4. Get-Disk.BusType 是唯一能可靠区分 USB 外置盘与内置 NVMe/SATA 盘的属性
//  5. 必须设置 [Console]::OutputEncoding = UTF8, 否则中文卷标被 GBK 编码后 Node 侧解析成乱码
//  6. ConvertTo-Json 必须用 -InputObject 传参: 管道写法在空数组时输出空字符串而非 '[]',
//     会让上层静默降级, 把真实故障掩盖成「没有设备」
const PS_SCAN_SCRIPT = `
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
$out = @()
$seen = @{}

# --- 第 1 层: DriveType=2(可移动)/5(光驱), 覆盖 U 盘与光驱 ---
foreach ($ld in @(Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 2 -or $_.DriveType -eq 5 })) {
    if ($seen.ContainsKey($ld.DeviceID)) { continue }
    $seen[$ld.DeviceID] = $true
    $out += [PSCustomObject]@{
        DeviceID   = $ld.DeviceID
        VolumeName = $ld.VolumeName
        Size       = $ld.Size
        BusType    = 'Removable'
        Model      = $null
        Kind       = $(if ($ld.DriveType -eq 5) { 'cdrom' } else { 'removable' })
    }
}

# --- 第 2 层: Get-Disk 按 BusType 精确判定, 覆盖 USB 移动硬盘 ---
$storageOk = $true
try {
    foreach ($d in @(Get-Disk | Where-Object { $_.BusType -eq 'USB' -or $_.BusType -eq '1394' })) {
        foreach ($p in @(Get-Partition -DiskNumber $d.Number)) {
            if ($p.DriveLetter -eq 0) { continue }
            $letter = ([char]$p.DriveLetter) + ':'
            if ($seen.ContainsKey($letter)) { continue }
            $ld  = Get-CimInstance Win32_LogicalDisk -Filter ("DeviceID='" + $letter + "'")
            $vol = Get-Volume -Partition $p
            $nm  = if ($ld -and $ld.VolumeName) { $ld.VolumeName } elseif ($vol) { $vol.FileSystemLabel } else { $null }
            $sz  = if ($ld -and $ld.Size) { $ld.Size } else { $p.Size }
            $seen[$letter] = $true
            $out += [PSCustomObject]@{
                DeviceID   = $letter
                VolumeName = $nm
                Size       = $sz
                BusType    = [string]$d.BusType
                Model      = $d.FriendlyName
                Kind       = 'external'
            }
        }
    }
} catch {
    $storageOk = $false
}

# --- 第 3 层: WQL ASSOCIATORS OF, Storage 模块缺失或结果为空时兜底 (兼容 Win7+) ---
if (-not $storageOk -or $out.Count -eq 0) {
    foreach ($dd in @(Get-CimInstance Win32_DiskDrive)) {
        $isUsb = ($dd.InterfaceType -eq 'USB') -or
                 ($dd.PNPDeviceID -like 'USBSTOR*') -or
                 ($dd.MediaType -eq 'Removable media') -or
                 ($dd.MediaType -eq 'External hard disk media')
        if (-not $isUsb) { continue }
        $q1 = "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + $dd.DeviceID + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition"
        foreach ($p in @(Get-CimInstance -Query $q1)) {
            $q2 = "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + $p.DeviceID + "'} WHERE AssocClass=Win32_LogicalDiskToPartition"
            foreach ($l in @(Get-CimInstance -Query $q2)) {
                if ($seen.ContainsKey($l.DeviceID)) { continue }
                $seen[$l.DeviceID] = $true
                $out += [PSCustomObject]@{
                    DeviceID   = $l.DeviceID
                    VolumeName = $l.VolumeName
                    Size       = $l.Size
                    BusType    = $dd.InterfaceType
                    Model      = $dd.Model
                    Kind       = 'external'
                }
            }
        }
    }
}

ConvertTo-Json -InputObject @($out) -Depth 4 -Compress
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

/** 解析 PowerShell JSON 输出 */
function parseWmiOutput(stdout: string): UsbDrive[] {
  try {
    const text = stdout.trim()
    // 空输出 / 空数组都视为「无设备」; 非空但既非数组也非对象时记日志后返回空
    if (!text || text === '' || text === 'null' || text === '[]') return []

    let data: any
    try {
      data = JSON.parse(text)
    } catch {
      // 兜底: 尝试从夹杂告警信息的输出中提取 JSON 片段
      const jsonMatch = text.match(/\[[\s\S]*\]/) || text.match(/\{[\s\S]*\}/)
      if (jsonMatch) data = JSON.parse(jsonMatch[0])
      else return []
    }

    // -InputObject 保证输出恒为数组, 单条时不会退化成对象; 此处仍保留兼容
    const arr = Array.isArray(data) ? data : (data ? [data] : [])
    return arr
      .filter((d: any) => d && d.DeviceID)
      .map((d: any) => {
        // Kind 由脚本给出: cdrom / removable / external
        let type = 'usb'
        if (d.Kind === 'cdrom') type = 'cdrom'
        else if (d.Kind === 'removable') type = 'removable'
        else if (d.Kind === 'external') type = 'external'
        // 兼容旧字段 DriveType
        else if (d.DriveType === 2) type = 'removable'
        else if (d.DriveType === 5) type = 'cdrom'

        return {
          drive: d.DeviceID,
          label: d.VolumeName || undefined,
          type,
          size: d.Size ? formatBytes(Number(d.Size)) : undefined,
          model: d.Model || undefined
        }
      })
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

/** D15: 按 config.usb.ignoreTypes 过滤设备类型 —— 被忽略的类型不进入状态比对/广播/UI */
function filterIgnored(drives: UsbDrive[]): UsbDrive[] {
  if (ignoreTypes.length === 0) return drives
  return drives.filter(d => !ignoreTypes.includes(d.type))
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

/**
 * 单次扫描 (内置三层降级, 只需一次 PowerShell 调用)
 * 原先三次独立调用中, 第 2 层只查 DriveType 2/5 (移动硬盘为 3, 必然落空),
 * 第 3 层 wmic 在 Win10 21H1+ / Win11 已被移除, 因此移动硬盘会一路降级到空结果。
 */
async function doScan(): Promise<UsbDrive[]> {
  try {
    const { stdout, stderr } = await execAsync(
      `powershell -NoProfile -NonInteractive -EncodedCommand "${toBase64Command(PS_SCAN_SCRIPT)}"`,
      { timeout: 15000, windowsHide: true, maxBuffer: 2 * 1024 * 1024 }
    )
    if (stderr?.trim()) log.warn('[USB] PS stderr:', stderr.trim().slice(0, 200))
    const results = parseWmiOutput(stdout)
    if (results.length > 0) {
      log.info(`[USB] Scan: ${results.length} device(s) -> ${results.map(r => `${r.drive}(${r.type})`).join(', ')}`)
    }
    return results
  } catch (e: any) {
    log.error('[USB] Scan failed:', e.message)
    return []
  }
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

  watcherStartedAt = Date.now()
  // (重)启动成功 → 退出降级轮询, 恢复低频巡检
  stopDegradedPolling()
  startHealthPolling()

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
    psWatcher = null
    if (!isRunning) return

    // 若监听器已稳定运行超过 60s, 视为偶发异常 (如系统休眠/唤醒), 重置退避计数
    if (Date.now() - watcherStartedAt > 60_000) watcherFailures = 0
    watcherFailures++

    // 指数退避重启: 5s → 10s → 20s → 40s → 60s 封顶, 防止 WMI 彻底损坏时无限快速重启
    const delay = Math.min(WATCHER_BACKOFF_MS * Math.pow(2, watcherFailures - 1), WATCHER_BACKOFF_MAX_MS)
    log.warn(`[USB] WMI watcher exited (code=${code}, failures=${watcherFailures}), restarting in ${delay / 1000}s; degraded polling active`)

    // 监听器空缺期间启用高频降级轮询, 保证设备仍能被识别
    startDegradedPolling()

    if (watcherRestartTimer) clearTimeout(watcherRestartTimer)
    watcherRestartTimer = setTimeout(() => {
      watcherRestartTimer = null
      startWmiWatcher()
    }, delay)
  })
}

/** 监听器健康时: 仅低频巡检 (兜底 WMI 事件路径静默失效), 空闲期不产生额外 PowerShell 进程 */
function startHealthPolling(): void {
  if (healthPollTimer) clearInterval(healthPollTimer)
  healthPollTimer = setInterval(() => void runScan({ notify: false }), FALLBACK_POLL_MS)
  updatePollDiag()
}

/** 监听器异常退出后: 高频降级轮询, 直至重启成功 */
function startDegradedPolling(): void {
  if (degradedPollTimer) return
  degradedPollTimer = setInterval(() => void runScan({ notify: false }), FALLBACK_POLL_MS_DEGRADED)
  updatePollDiag()
}

function stopDegradedPolling(): void {
  if (degradedPollTimer) {
    clearInterval(degradedPollTimer)
    degradedPollTimer = null
  }
  updatePollDiag()
}

function stopAllPolling(): void {
  if (healthPollTimer) {
    clearInterval(healthPollTimer)
    healthPollTimer = null
  }
  stopDegradedPolling()
}

function updatePollDiag(): void {
  currentPollMs = degradedPollTimer ? FALLBACK_POLL_MS_DEGRADED : healthPollTimer ? FALLBACK_POLL_MS : 0
}

function stopWmiWatcher(): void {
  if (psWatcher) {
    try { psWatcher.kill('SIGTERM') } catch { /* ignore */ }
    psWatcher = null
  }
  if (pendingScanTimer) {
    clearTimeout(pendingScanTimer)
    pendingScanTimer = null
  }
  if (watcherRestartTimer) {
    clearTimeout(watcherRestartTimer)
    watcherRestartTimer = null
  }
}

/**
 * 统一的「扫描结果 → 差异比对 → 广播」。
 *
 * 原先 handleUsbArrival / handleUsbRemoval / doFallbackPoll 三处各复制了一份约 35 行
 * 相同逻辑, 且写法已经漂移(arrival 用 `lastNotify && diff < DEBOUNCE` 跳过,
 * poll 用 `!lastNotify || diff >= DEBOUNCE` 放行, 语义相同但表述不同, 改一处容易漏另一处),
 * 现合并为单一实现。
 *
 * @param notify true = 事件驱动路径, 产生系统通知与提示音; false = 轮询路径, 静默更新
 */
function reconcile(results: UsbDrive[], opts: { notify: boolean }): void {
  const currentMap = new Map<string, UsbDrive>()
  for (const d of results) currentMap.set(d.drive, d)

  const tag = opts.notify ? '' : ' (poll)'

  for (const d of results) {
    if (lastDrives.has(d.drive)) continue

    const now = Date.now()
    const lastNotify = notifyHistory.get(d.drive)
    if (lastNotify && now - lastNotify < DEBOUNCE_MS) {
      log.info(`[USB] Debounced: ${d.drive}`)
      continue
    }
    notifyHistory.set(d.drive, now)

    log.info(`[USB] >>> ARRIVED${tag}: ${d.drive} (${d.label || 'unknown'}, ${d.size || '?'})`)
    UsbService.broadcast(IPC_CHANNELS['usb:arrived'], {
      drive: d.drive,
      label: d.label,
      type: d.type,
      size: d.size,
      at: now
    })
    if (opts.notify) {
      showUsbNotification(d.drive, d.label, d.size)
      playUsbSound()
    }
  }

  for (const [drive, info] of lastDrives) {
    if (currentMap.has(drive)) continue
    log.info(`[USB] <<< REMOVED${tag}: ${drive}`)
    notifyHistory.delete(drive)
    UsbService.broadcast(IPC_CHANNELS['usb:removed'], {
      drive,
      label: info.label,
      type: info.type,
      at: Date.now()
    })
  }

  lastDrives = currentMap
  trimNotifyHistory()
}

/** notifyHistory 容量控制: 超出上限时丢弃最旧条目 (Map 保持插入顺序) */
function trimNotifyHistory(): void {
  const excess = notifyHistory.size - NOTIFY_HISTORY_MAX
  if (excess <= 0) return
  const it = notifyHistory.keys()
  for (let i = 0; i < excess; i++) {
    const next = it.next()
    if (next.done) break
    notifyHistory.delete(next.value)
  }
}

/**
 * 串行化扫描入口: 同一时刻只允许一次扫描在跑;
 * 扫描期间到达的请求合并为收尾后的一次补跑, 而不是被丢弃。
 */
async function runScan(opts: { notify: boolean }): Promise<void> {
  if (isScanning) {
    // 期间只要有过事件驱动的请求, 补跑就带通知 (轮询请求不升级为通知)
    rescanQueued = rescanQueued || opts.notify
    return
  }

  isScanning = true
  try {
    do {
      rescanQueued = false
      const results = filterIgnored(await doScan())
      reconcile(results, opts)
    } while (rescanQueued)
  } catch (e: any) {
    log.error('[USB] Scan/reconcile error:', e.message)
  } finally {
    isScanning = false
  }
}

/** 合并窗口内触发一次扫描: 一次物理插拔常触发多个 Win32_VolumeChangeEvent */
function scheduleScan(delayMs: number, notify: boolean): void {
  if (pendingScanTimer) clearTimeout(pendingScanTimer)
  pendingScanTimer = setTimeout(() => {
    pendingScanTimer = null
    void runScan({ notify })
  }, delayMs)
}

/** 插入事件处理 */
function handleUsbArrival(drive: string): void {
  log.debug(`[USB] arrival event (${drive}), scheduling scan in ${ARRIVAL_DEBOUNCE_MS}ms`)
  scheduleScan(ARRIVAL_DEBOUNCE_MS, true)
}

/** 移除事件处理 */
function handleUsbRemoval(drive: string): void {
  log.debug(`[USB] removal event (${drive}), scheduling scan in ${REMOVAL_DELAY_MS}ms`)
  // 延后一点再扫描, 等系统完成卷卸载
  scheduleScan(REMOVAL_DELAY_MS, true)
}

export const UsbService = {
  async start(cfg: SidekickConfig): Promise<void> {
    if (isRunning) return
    if (!cfg.usb.enabled) {
      log.info('[USB] Disabled by config')
      return
    }

    isRunning = true
    watcherFailures = 0

    // D15: 读取忽略类型配置
    ignoreTypes = Array.isArray(cfg.usb.ignoreTypes) ? cfg.usb.ignoreTypes : []
    if (ignoreTypes.length > 0) log.info(`[USB] ignoreTypes: ${ignoreTypes.join(', ')}`)

    // 初始扫描
    try {
      const initial = filterIgnored(await doScan())
      lastDrives = new Map()
      for (const d of initial) lastDrives.set(d.drive, d)
      log.info(`[USB] Initial scan: ${initial.length} device(s)`)
    } catch (e: any) {
      log.error('[USB] Initial scan failed:', e.message)
    }

    // 主路径: WMI 事件驱动; 轮询策略由监听器生命周期接管
    // (健康时仅 5 分钟巡检, 退出后转 8s 降级轮询 + 指数退避重启)
    startWmiWatcher()

    log.info('[USB] Service started v7 (BusType detection + WQL ASSOCIATORS fallback, event-driven + health-checked polling)')
  },

  stop(): void {
    isRunning = false
    stopWmiWatcher()
    stopAllPolling()
    lastDrives.clear()
    notifyHistory.clear()
    log.info('[USB] Service stopped')
  },

  async scan(): Promise<UsbDrive[]> {
    isScanning = true
    try {
      const results = filterIgnored(await doScan())
      lastDrives = new Map()
      for (const d of results) lastDrives.set(d.drive, d)
      return results
    } finally {
      isScanning = false
    }
  },

  list(): UsbDrive[] {
    return Array.from(lastDrives.values())
  },

  broadcast(channel: string, data: DriveEvent): void {
    for (const win of BrowserWindow.getAllWindows()) {
      if (!win.isDestroyed() && win.webContents && !win.webContents.isDestroyed()) {
        win.webContents.send(channel, data)
      }
    }
  },

  getCurrentDrives(): Map<string, UsbDrive> {
    return new Map(lastDrives)
  },

  getDiagnostics(): object {
    return {
      isRunning,
      isScanning,
      watcherAlive: psWatcher !== null && !psWatcher.killed,
      watcherFailures,
      lastDrivesCount: lastDrives.size,
      drives: Array.from(lastDrives.values()),
      notifyHistoryCount: notifyHistory.size,
      currentPollMs,
      degradedPolling: degradedPollTimer !== null,
      ignoreTypes,
      version: 'v7-bustype-detection'
    }
  }
}

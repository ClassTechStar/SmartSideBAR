#!/usr/bin/env node
/**
 * P2-8 性能预算实测脚本
 *
 * 用法 (在目标教学一体机上):
 *   node scripts/bench-p28.mjs [--exe "路径\SmartSideBAR.exe"] [--duration 600]
 *
 * 前置条件:
 *   - Node.js ≥ 20
 *   - SmartSideBAR 尚未运行 (脚本会自动启动并测量冷启动)
 *   - 以管理员或普通用户身份运行均可
 *
 * 输出: 控制台表格 + JSON 报告文件 (dist/perf-report.json)
 *
 * 测量指标:
 *   1. 冷启动时间 (exe 启动 → 侧边栏窗口出现)
 *   2. 空闲内存 (静置 5 分钟后读 Private Working Set)
 *   3. 空闲 CPU (5 分钟平均 CPU 百分比)
 *   4. 10 分钟内 PowerShell 进程创建次数
 *
 * 建议在「干净重启后」且「无其他高负载应用」的环境下运行, 以获得可复现数据。
 */

import { execSync, spawn } from 'node:child_process'
import { writeFileSync, existsSync } from 'node:fs'
import { join } from 'node:path'

// ---- CLI 参数解析 ----
const args = process.argv.slice(2)
function getArg(name, fallback) {
  const idx = args.indexOf(name)
  return idx >= 0 && args[idx + 1] ? args[idx + 1] : fallback
}

const EXE_PATH = getArg('--exe', '')
const WATCH_DURATION = parseInt(getArg('--duration', '600'), 10) // 秒, 默认 10 分钟
const IDLE_PHASE = 300 // 秒, 静置阶段 (5 分钟)
const REPORT_PATH = getArg('--out', 'dist/perf-report.json')

// ---- 工具函数 ----
function ps(script) {
  try {
    const r = execSync(`powershell -NoProfile -NonInteractive -Command "${script}"`, {
      encoding: 'utf8',
      timeout: 15_000,
      windowsHide: true
    })
    return r.trim()
  } catch (e) {
    return ''
  }
}

/** 获取 SmartSideBAR.exe 的 PID 列表 */
function getPids() {
  const raw = ps(`Get-Process -Name "SmartSideBAR" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id`)
  if (!raw) return []
  return raw.split(/\r?\n/).map(s => parseInt(s.trim(), 10)).filter(n => !isNaN(n))
}

/** 获取进程的 Working Set (MB) */
function getWorkingSetMb(pid) {
  const raw = ps(`(Get-Process -Id ${pid} -ErrorAction SilentlyContinue).WorkingSet64 / 1MB`)
  const v = parseFloat(raw)
  return isNaN(v) ? null : Math.round(v * 10) / 10
}

/** 获取进程的 CPU 百分比 (采样间隔 5s, 返回总百分比) */
function getCpuPercent(pids, sampleSec = 5) {
  if (pids.length === 0) return null
  const idList = pids.join(',')
  // 用 Get-Counter 采样两次, 计算 delta
  const raw = ps(`
$cpu1 = (Get-Process -Id ${pids[0]} -ErrorAction SilentlyContinue).CPU
Start-Sleep -Seconds ${sampleSec}
$cpu2 = (Get-Process -Id ${pids[0]} -ErrorAction SilentlyContinue).CPU
$delta = $cpu2 - $cpu1
if ($delta -lt 0) { $delta = 0 }
$pct = ($delta / ${sampleSec}) * 100 / (Get-CimInstance Win32_Processor | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum
[Math]::Round($pct, 2)
`)
  const v = parseFloat(raw)
  return isNaN(v) ? null : v
}

/** 统计当前系统中 powershell.exe 进程数 */
function getPsProcessCount() {
  const raw = ps(`(Get-Process -Name "powershell" -ErrorAction SilentlyContinue).Count`)
  const v = parseInt(raw, 10)
  return isNaN(v) ? 0 : v
}

/**
 * USB 移动硬盘识别验证 (复用 usb.ts PS_SCAN_SCRIPT 逻辑)
 * 执行一次扫描, 检查是否能识别到至少一块 USB 外置设备 (BusType=USB)
 * 返回 { pass, devices[], externalCount }
 */
function verifyUsbRecognition() {
  const script = `
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'SilentlyContinue'
$out = @()
$seen = @{}

# 第 1 层: DriveType=2 (可移动) / 5 (光驱)
foreach ($ld in @(Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DriveType -eq 2 -or $_.DriveType -eq 5 })) {
  if ($seen.ContainsKey($ld.DeviceID)) { continue }
  $seen[$ld.DeviceID] = $true
  $out += [PSCustomObject]@{
    DeviceID = $ld.DeviceID; VolumeName = $ld.VolumeName; Size = $ld.Size
    BusType  = 'Removable'; Model = $null
    Kind     = $(if ($ld.DriveType -eq 5) { 'cdrom' } else { 'removable' })
  }
}

# 第 2 层: Get-Disk BusType=USB / 1394
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
        DeviceID = $letter; VolumeName = $nm; Size = $sz
        BusType  = [string]$d.BusType; Model = $d.FriendlyName; Kind = 'external'
      }
    }
  }
} catch {}

# 第 3 层: WQL ASSOCIATORS OF 兜底
if ($out.Count -eq 0) {
  foreach ($dd in @(Get-CimInstance Win32_DiskDrive)) {
    $isUsb = ($dd.InterfaceType -eq 'USB') -or ($dd.PNPDeviceID -like 'USBSTOR*') -or
             ($dd.MediaType -eq 'Removable media') -or ($dd.MediaType -eq 'External hard disk media')
    if (-not $isUsb) { continue }
    $q1 = "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + $dd.DeviceID + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition"
    foreach ($p in @(Get-CimInstance -Query $q1)) {
      $q2 = "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + $p.DeviceID + "'} WHERE AssocClass=Win32_LogicalDiskToPartition"
      foreach ($l in @(Get-CimInstance -Query $q2)) {
        if ($seen.ContainsKey($l.DeviceID)) { continue }
        $seen[$l.DeviceID] = $true
        $out += [PSCustomObject]@{
          DeviceID = $l.DeviceID; VolumeName = $l.VolumeName; Size = $l.Size
          BusType  = $dd.InterfaceType; Model = $dd.Model; Kind = 'external'
        }
      }
    }
  }
}

ConvertTo-Json -InputObject @($out) -Depth 4 -Compress
`

  const raw = ps(script)
  if (!raw) {
    console.log('   ⚠️  扫描无输出 (可能无 USB 设备连接)')
    return { pass: null, devices: [], externalCount: 0 }
  }

  let devices = []
  try {
    const parsed = JSON.parse(raw)
    devices = Array.isArray(parsed) ? parsed : [parsed]
  } catch {
    const m = raw.match(/\[[\s\S]*\]/) || raw.match(/\{[\s\S]*\}/)
    if (m) {
      try { devices = JSON.parse(m[0]) } catch { devices = [] }
    }
  }

  const external = devices.filter(d => d.Kind === 'external')
  const removable = devices.filter(d => d.Kind === 'removable')
  const cdrom = devices.filter(d => d.Kind === 'cdrom')

  if (devices.length === 0) {
    console.log('   ⚠️  未检测到任何 USB 存储设备')
    console.log('       请插入 U 盘或移动硬盘后重新运行')
    return { pass: null, devices: [], externalCount: 0 }
  }

  console.log(`   扫描到 ${devices.length} 个设备:`)
  for (const d of devices) {
    const sizeMB = d.Size ? Math.round(Number(d.Size) / 1024 / 1024) + ' MB' : '?'
    const label = d.VolumeName || d.Model || '(无标签)'
    console.log(`     ${d.DeviceID || '?'}  ${label}  ${sizeMB}  [${d.Kind}]  BusType=${d.BusType || '?'}`)
  }

  if (external.length > 0) {
    console.log(`   ✅ ${external.length} 块判定为 external (移动硬盘), BusType=USB 识别成功`)
  } else if (removable.length > 0) {
    console.log(`   ℹ️  仅有 removable (U盘), 未发现 external (移动硬盘)`)
  }

  return {
    pass: devices.length > 0,
    devices: devices.map(d => ({ id: d.DeviceID, label: d.VolumeName, kind: d.Kind, busType: d.BusType, model: d.Model })),
    externalCount: external.length
  }
}

/** 统计某个时间窗口内 PowerShell 进程的创建次数 (通过 WMI 事件轮询, 采样间隔 5s) */
function startPsCreationWatcher() {
  let count = 0
  const seen = new Set()

  // 初始快照: 已有的 PS 进程
  const raw = ps(`Get-Process -Name "powershell" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id`)
  if (raw) {
    raw.split(/\r?\n/).forEach(s => {
      const pid = parseInt(s.trim(), 10)
      if (!isNaN(pid)) seen.add(pid)
    })
  }

  const timer = setInterval(() => {
    const raw2 = ps(`Get-Process -Name "powershell" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id`)
    if (!raw2) return
    const current = new Set()
    raw2.split(/\r?\n/).forEach(s => {
      const pid = parseInt(s.trim(), 10)
      if (!isNaN(pid)) {
        current.add(pid)
        if (!seen.has(pid)) {
          seen.add(pid)
          count++
        }
      }
    })
  }, 5_000) // 每 5 秒扫描一次 (避免频繁调用)

  return {
    getCount: () => count,
    stop: () => clearInterval(timer)
  }
}

/** 等待进程启动, 返回 PID 和耗时 (ms) */
function waitForProcess(exeName, timeoutSec = 30) {
  const t0 = Date.now()
  while (Date.now() - t0 < timeoutSec * 1000) {
    const pids = getPids()
    if (pids.length > 0) return { pid: pids[0], elapsed: Date.now() - t0 }
    // 也可按进程名匹配
    const raw = ps(`Get-Process -Name "${exeName.replace('.exe','')}" -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Id`)
    const pid = parseInt(raw, 10)
    if (!isNaN(pid)) return { pid, elapsed: Date.now() - t0 }
    execSync('timeout /t 1 /nobreak >nul', { shell: 'cmd.exe', windowsHide: true })
  }
  return null
}

/** 格式化秒为 mm:ss */
function fmtTime(sec) {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}

// ---- 主流程 ----
async function main() {
  console.log('╔══════════════════════════════════════════════╗')
  console.log('║  SmartSideBAR P2-8 性能预算 + USB 识别实测  ║')
  console.log('╚══════════════════════════════════════════════╝')
  console.log()

  // 确定 exe 路径
  let exePath = EXE_PATH
  if (!exePath) {
    // 尝试默认安装路径
    const candidates = [
      join(process.env.LOCALAPPDATA || '', 'Programs', 'SmartSideBAR', 'SmartSideBAR.exe'),
      join(process.env.ProgramFiles || '', 'SmartSideBAR', 'SmartSideBAR.exe'),
      join(process.env.ProgramFiles || '', 'SmartSideBAR', 'SmartSideBAR.exe'),
    ]
    for (const c of candidates) {
      if (existsSync(c)) { exePath = c; break }
    }
    if (!exePath) {
      // 尝试 dist/win-unpacked (开发构建)
      const devPath = join(process.cwd(), 'dist', 'win-unpacked', 'SmartSideBAR.exe')
      if (existsSync(devPath)) exePath = devPath
    }
  }

  if (!exePath || !existsSync(exePath)) {
    console.error('❌ 未找到 SmartSideBAR.exe')
    console.error('   请用 --exe "路径\\SmartSideBAR.exe" 指定')
    console.error('   或先运行 npm run win 构建后再测试')
    process.exit(1)
  }

  console.log(`📦 exe: ${exePath}`)
  console.log(`⏱️  测试时长: ${fmtTime(WATCH_DURATION)} (静置 ${fmtTime(IDLE_PHASE)})`)
  console.log()

  // ---- 阶段 0: 确认未运行 ----
  const existing = getPids()
  if (existing.length > 0) {
    console.log('⚠️  SmartSideBAR 已在运行 (PID:', existing.join(', '), ')')
    console.log('   将跳过冷启动测量, 直接进入空闲观测阶段')
  }

  // ---- 阶段 1: 冷启动 ----
  let coldStartMs = null
  if (existing.length === 0) {
    console.log('🚀 [阶段 1/4] 冷启动...')
    const t0 = Date.now()
    const child = spawn(exePath, [], { detached: true, stdio: 'ignore', windowsHide: true })
    child.unref()

    // 等待进程出现
    const result = waitForProcess('SmartSideBAR', 30)
    if (result) {
      coldStartMs = result.elapsed
      console.log(`   ✅ 冷启动 ${coldStartMs} ms (PID ${result.pid})`)
      if (coldStartMs > 3000) {
        console.log(`   ⚠️  超出预算 (≤ 3000 ms)`)
      }
    } else {
      console.log('   ❌ 30 秒内未检测到进程启动')
    }
  } else {
    coldStartMs = null // 跳过
  }

  // ---- 阶段 2: 空闲观测 ----
  console.log()
  console.log(`⏳ [阶段 2/4] 空闲观测 ${fmtTime(IDLE_PHASE)} (期间请勿操作)...`)

  const psWatcher = startPsCreationWatcher()
  const cpuSamples = []
  const memSamples = []
  const phaseStart = Date.now()
  const sampleInterval = 15_000 // 每 15 秒采样一次
  let sampleCount = 0

  while (Date.now() - phaseStart < IDLE_PHASE * 1000) {
    const pids = getPids()
    if (pids.length === 0) {
      console.log('   ⚠️  SmartSideBAR 进程已退出, 跳过空闲观测')
      break
    }

    const mem = getWorkingSetMb(pids[0])
    if (mem !== null) memSamples.push(mem)

    const cpu = getCpuPercent(pids, 5)
    if (cpu !== null) cpuSamples.push(cpu)

    sampleCount++
    const elapsed = Math.round((Date.now() - phaseStart) / 1000)
    const lastMem = memSamples.length > 0 ? memSamples[memSamples.length - 1] : '?'
    const lastCpu = cpuSamples.length > 0 ? cpuSamples[cpuSamples.length - 1] : '?'
    process.stdout.write(`\r   [${fmtTime(elapsed)}/${fmtTime(IDLE_PHASE)}] 内存=${lastMem}MB  CPU=${lastCpu}%  PS创建=${psWatcher.getCount()}次  `)

    // 等待采样间隔 (扣除 CPU 采样的 ~5s)
    const waitMs = Math.max(0, sampleInterval - 5000)
    if (waitMs > 0) await new Promise(r => setTimeout(r, waitMs))
  }

  console.log()
  psWatcher.stop()

  // ---- 阶段 3: USB 移动硬盘识别验证 ----
  console.log()
  console.log('🔌 [阶段 3/4] USB 移动硬盘识别验证...')

  const usbResult = verifyUsbRecognition()

  // ---- 阶段 4: 延长观测 (剩余时间) ----
  const remainingSec = WATCH_DURATION - IDLE_PHASE
  if (remainingSec > 0) {
    console.log()
    console.log(`⏳ [阶段 4/4] 延续观测 ${fmtTime(remainingSec)} (继续计数 PS 进程)...`)

    const psWatcher2 = startPsCreationWatcher()
    // 阶段 2 的进程快照已包含, 需排除
    const phase2Start = Date.now()
    await new Promise(r => setTimeout(r, remainingSec * 1000))
    psWatcher2.stop()

    // 合并两个阶段的 PS 创建次数 (去重)
    const totalPsCreated = psWatcher.getCount() + psWatcher2.getCount()
    console.log()
    console.log(`   PS 进程创建 (阶段 2+3): ${totalPsCreated} 次`)
  }

  const totalPsCreated = psWatcher.getCount()

  // ---- 阶段 5: 最终采样 ----
  console.log()
  console.log('📊 [阶段 5/5] 最终采样...')

  const finalPids = getPids()
  const finalMem = finalPids.length > 0 ? getWorkingSetMb(finalPids[0]) : null
  const finalCpu = finalPids.length > 0 ? getCpuPercent(finalPids, 10) : null
  const finalPsCount = getPsProcessCount()

  // ---- 汇总 ----
  const avgMem = memSamples.length > 0 ? Math.round(memSamples.reduce((a, b) => a + b, 0) / memSamples.length * 10) / 10 : null
  const maxMem = memSamples.length > 0 ? Math.max(...memSamples) : null
  const avgCpu = cpuSamples.length > 0 ? Math.round(cpuSamples.reduce((a, b) => a + b, 0) / cpuSamples.length * 100) / 100 : null
  const maxCpu = cpuSamples.length > 0 ? Math.max(...cpuSamples) : null

  const report = {
    timestamp: new Date().toISOString(),
    exePath,
    watchDuration: WATCH_DURATION,
    idlePhase: IDLE_PHASE,
    results: {
      coldStart: {
        value: coldStartMs,
        unit: 'ms',
        budget: 3000,
        pass: coldStartMs !== null ? coldStartMs <= 3000 : null
      },
      idleMemory: {
        avg: avgMem,
        max: maxMem,
        final: finalMem,
        unit: 'MB',
        budget: 220,
        samples: memSamples.length,
        pass: avgMem !== null ? avgMem <= 220 : null
      },
      idleCpu: {
        avg: avgCpu,
        max: maxCpu,
        final: finalCpu,
        unit: '%',
        budget: 2,
        samples: cpuSamples.length,
        pass: avgCpu !== null ? avgCpu < 2 : null
      },
      psProcessCreation: {
        value: totalPsCreated,
        current: finalPsCount,
        unit: '次/10分钟',
        budget: 3,
        pass: totalPsCreated <= 3
      },
      usbRecognition: {
        devices: usbResult.devices,
        externalCount: usbResult.externalCount,
        pass: usbResult.pass
      }
    }
  }

  console.log()
  console.log('╔══════════════════════════════════════════════╗')
  console.log('║              测试结果汇总                   ║')
  console.log('╠══════════════════════════════════════════════╣')

  const rows = [
    ['冷启动',      coldStartMs !== null ? `${coldStartMs} ms` : '跳过(已运行)', '≤ 3000 ms',  report.results.coldStart.pass],
    ['空闲内存(均)', avgMem !== null ? `${avgMem} MB` : 'N/A',                   '≤ 220 MB',   report.results.idleMemory.pass],
    ['空闲内存(峰)', maxMem !== null ? `${maxMem} MB` : 'N/A',                   '≤ 220 MB',   maxMem !== null ? maxMem <= 220 : null],
    ['空闲 CPU(均)', avgCpu !== null ? `${avgCpu} %` : 'N/A',                    '< 2 %',      report.results.idleCpu.pass],
    ['空闲 CPU(峰)', maxCpu !== null ? `${maxCpu} %` : 'N/A',                    '< 2 %',      maxCpu !== null ? maxCpu < 2 : null],
    ['PS 进程创建',  `${totalPsCreated} 次`,                                      '≤ 3 次/10min', report.results.psProcessCreation.pass],
    ['当前 PS 进程',  `${finalPsCount}`,                                          '—',          null],
    ['USB 设备识别',  `${usbResult.devices.length} 个 (${usbResult.externalCount} 移动硬盘)`, '≥ 1 个设备', usbResult.pass],
  ]

  for (const [label, value, budget, pass] of rows) {
    const status = pass === null ? '  ' : pass ? '✅' : '❌'
    const line = `║  ${status} ${label.padEnd(14)} ${value.padEnd(16)} ${budget.padEnd(12)} ║`
    console.log(line)
  }

  console.log('╠══════════════════════════════════════════════╣')
  const passCount = rows.filter(r => r[3] === true).length
  const failCount = rows.filter(r => r[3] === false).length
  const skipCount = rows.filter(r => r[3] === null).length
  console.log(`║  总计: ${passCount} 达标 / ${failCount} 超标 / ${skipCount} 无数据             ║`)
  console.log('╚══════════════════════════════════════════════╝')

  // ---- 输出报告 ----
  try {
    const reportDir = join(process.cwd(), 'dist')
    writeFileSync(join(reportDir, 'perf-report.json'), JSON.stringify(report, null, 2), 'utf8')
    console.log()
    console.log(`📄 报告已保存: ${REPORT_PATH}`)
  } catch (e) {
    console.log()
    console.log('⚠️  保存报告失败 (dist/ 目录可能不存在):', e.message)
    // 尝试写到当前目录
    try {
      writeFileSync('perf-report.json', JSON.stringify(report, null, 2), 'utf8')
      console.log('📄 报告已保存: ./perf-report.json')
    } catch {
      console.log('❌ 无法保存报告')
    }
  }

  // ---- 裁剪建议 ----
  if (failCount > 0) {
    console.log()
    console.log('⚠️  超标项裁剪建议:')
    if (report.results.coldStart.pass === false) {
      console.log('  - 冷启动超标: 检查 sharp 加载时间, 考虑按需加载; 检查注册表写入耗时')
    }
    if (report.results.idleMemory.pass === false) {
      console.log('  - 内存超标: 检查 BrowserWindow 数量; 考虑按需加载 sharp; 降低缩略图上限 (3840x2160→1920x1080)')
    }
    if (report.results.idleCpu.pass === false) {
      console.log('  - CPU 超标: 检查 USB 轮询频率 (应为 5min/次); 检查 IME 守护进程是否频繁重启')
    }
    if (report.results.psProcessCreation.pass === false) {
      console.log('  - PS 进程创建超标: 检查 USB WMI 事件监听器健康状态; 检查 IME 守护进程退出次数')
    }
  }

  console.log()
  console.log('✅ 测试完成')
}

main().catch(e => {
  console.error('❌ 测试脚本异常:', e.message)
  process.exit(1)
})

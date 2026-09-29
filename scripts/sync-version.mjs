#!/usr/bin/env node
// scripts/sync-version.mjs —— A3 版本号单一事实源
// 从 VERSION 文件读取版本号, 同步到:
//   - package.json (Electron 侧)
//   - Directory.Build.props (.NET 侧)
//   - installer/SmartSideBAR-v1.3.iss (Inno Setup)
//   - build/installer.nsh (NSIS 注册表版本, Electron 侧遗留)
//
// 用法: node scripts/sync-version.mjs [--check]
//   --check: 仅检查是否一致, 不写入 (CI 用)

import { readFileSync, writeFileSync } from 'fs'
import { join, dirname } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const root = join(__dirname, '..')
const checkOnly = process.argv.includes('--check')

// 1. 读取 VERSION (单一事实源)
const version = readFileSync(join(root, 'VERSION'), 'utf-8').trim()
const versionParts = version.split('.')
while (versionParts.length < 4) versionParts.push('0')
const version4 = versionParts.join('.')

console.log(`[sync-version] VERSION = ${version} (${version4})`)

const updates = []

// 2. package.json
function syncPackageJson() {
  const path = join(root, 'package.json')
  const raw = readFileSync(path, 'utf-8')
  const pkg = JSON.parse(raw)
  if (pkg.version !== version) {
    const old = pkg.version
    if (!checkOnly) {
      pkg.version = version
      writeFileSync(path, JSON.stringify(pkg, null, 2) + '\n')
    }
    updates.push(`package.json: ${old} -> ${version}`)
  }
}

// 3. Directory.Build.props
function syncDirectoryBuildProps() {
  const path = join(root, 'Directory.Build.props')
  let raw = readFileSync(path, 'utf-8')
  const versionRe = /<Version>[^<]*<\/Version>/
  const match = raw.match(versionRe)
  if (match) {
    const current = match[0].replace(/<\/?Version>/g, '')
    if (current !== version) {
      if (!checkOnly) {
        raw = raw.replace(versionRe, `<Version>${version}</Version>`)
        writeFileSync(path, raw)
      }
      updates.push(`Directory.Build.props: ${current} -> ${version}`)
    }
  }
}

// 4. Avalonia.csproj —— 移除本地 Version 覆盖, 继承 Directory.Build.props
function syncAvaloniaCsproj() {
  const path = join(root, 'src/SmartSideBAR.Avalonia/SmartSideBAR.Avalonia.csproj')
  let raw = readFileSync(path, 'utf-8')
  // 移除硬编码 Version/AssemblyVersion/FileVersion (由 Directory.Build.props 统一)
  const before = raw
  raw = raw.replace(/^\s*<Version>[^<]*<\/Version>\s*$/m, '')
  raw = raw.replace(/^\s*<AssemblyVersion>[^<]*<\/AssemblyVersion>\s*$/m, '')
  raw = raw.replace(/^\s*<FileVersion>[^<]*<\/FileVersion>\s*$/m, '')
  if (raw !== before) {
    if (!checkOnly) writeFileSync(path, raw)
    updates.push('SmartSideBAR.Avalonia.csproj: removed local Version overrides (inherit Directory.Build.props)')
  }
}

// 5. Inno Setup script
function syncInnoScript() {
  const path = join(root, 'installer/SmartSideBAR-v1.3.iss')
  let raw = readFileSync(path, 'utf-8')
  const before = raw
  raw = raw.replace(/#define AppVersion "[^"]*"/, `#define AppVersion "${version}"`)
  raw = raw.replace(/VersionInfoVersion=[\d.]+/, `VersionInfoVersion=${version4}`)
  raw = raw.replace(/VersionInfoProductVersion=[\d.]+/, `VersionInfoProductVersion=${version4}`)
  raw = raw.replace(/OutputBaseFilename=SmartSideBAR-v[\d.]+-Setup/, `OutputBaseFilename=SmartSideBAR-v${version}-Setup`)
  if (raw !== before) {
    if (!checkOnly) writeFileSync(path, raw)
    updates.push('SmartSideBAR-v1.3.iss: AppVersion/VersionInfo synced')
  }
}

// 6. build/installer.nsh (Electron 遗留, 注册表版本)
function syncInstallerNsh() {
  const path = join(root, 'build/installer.nsh')
  let raw = readFileSync(path, 'utf-8')
  const before = raw
  raw = raw.replace(/"Version" "1\.\d+\.\d+"/g, `"Version" "${version}"`)
  if (raw !== before) {
    if (!checkOnly) writeFileSync(path, raw)
    updates.push('installer.nsh: registry Version synced')
  }
}

syncPackageJson()
syncDirectoryBuildProps()
syncAvaloniaCsproj()
syncInnoScript()
syncInstallerNsh()

if (updates.length === 0) {
  console.log('[sync-version] All versions already in sync.')
} else {
  console.log('[sync-version] Updated:')
  for (const u of updates) console.log(`  - ${u}`)
  if (checkOnly) {
    console.error('[sync-version] CHECK FAILED: versions out of sync')
    process.exit(1)
  }
}

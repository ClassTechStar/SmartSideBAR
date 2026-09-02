#!/usr/bin/env node
// scripts/rebuild-native.mjs
//
// 用 node-gyp 针对当前 Electron 版本编译原生模块 (src/native/appbar.cc)。
// 旧版 electron-rebuild@3 在本机 (Node 26) 下会挂起且误用 Node 头文件, 故直接
// 调 node-gyp 并显式指定 Electron 头文件目录。
//
// 头文件缓存位置: %USERPROFILE%\.electron-gyp\<electronVersion>
// 若不存在, 用 --dist-url 从 electronjs.org 下载。

import { execSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import { homedir, platform, arch } from 'node:os'

const ROOT = join(import.meta.dirname, '..')

// 读取 Electron 版本
let electronVersion
try {
  const { createRequire } = await import('node:module')
  const require = createRequire(import.meta.url)
  electronVersion = require('electron/package.json').version
} catch {
  console.log('[rebuild-native] 未安装 electron, 跳过原生模块编译')
  process.exit(0)
}

// 非 Windows 平台无需编译 appbar (SHAppBarMessage 仅 Windows)
if (platform() !== 'win32') {
  console.log(`[rebuild-native] 平台 ${platform()} 跳过 appbar 原生模块 (仅 Windows)`)
  process.exit(0)
}

const devDir = join(homedir(), '.electron-gyp')
const headersDir = join(devDir, electronVersion)
const headersReady = existsSync(join(headersDir, 'include'))

const args = [
  'rebuild',
  `--target=${electronVersion}`,
  '--arch=x64',
  `--devdir="${devDir}"`,
  headersReady
    ? '' // 已缓存, node-gyp 会用 devdir
    : '--dist-url=https://www.electronjs.org/headers'
].filter(Boolean).join(' ')

const NATIVE_DIR = join(ROOT, 'native')

console.log(`[rebuild-native] node-gyp ${args}`)
try {
  execSync(`node-gyp ${args}`, {
    cwd: NATIVE_DIR,
    stdio: 'inherit',
    env: { ...process.env, GYP_MSVS_VERSION: process.env.GYP_MSVS_VERSION || '' }
  })
  console.log(`[rebuild-native] appbar.node 编译完成 (Electron ${electronVersion})`)
} catch (e) {
  console.error('[rebuild-native] 编译失败:', e.message)
  process.exit(1)
}

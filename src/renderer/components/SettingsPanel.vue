<template>
  <div class="panel-content">
    <div class="setting-item" @click="openSettings">
      <img :src="icon('settings')" />
      <span>打开设置窗口</span>
    </div>
    <div class="setting-item" @click="toggleAutoLaunch">
      <img :src="icon('lock')" />
      <span>{{ autoLaunch ? '关闭' : '启用' }}开机自启</span>
    </div>
    <div class="setting-item" :class="{ running: diagRunning }" @click="runDiagnostics">
      <img :src="icon('reminder')" />
      <span>{{ diagRunning ? '诊断中...' : '运行诊断' }}</span>
    </div>

    <!-- 诊断结果面板 -->
    <div v-if="diagResult" class="diag-panel">
      <div class="diag-header">
        <span class="diag-title">诊断结果</span>
        <span class="diag-time">{{ diagResult.timestamp }}</span>
      </div>

      <!-- 系统信息 -->
      <div class="diag-section">
        <div class="diag-section-title">系统信息</div>
        <div class="diag-info-grid">
          <div class="diag-info-item">
            <span class="info-label">版本</span>
            <span class="info-value">{{ diagResult.version }}</span>
          </div>
          <div class="diag-info-item">
            <span class="info-label">系统</span>
            <span class="info-value">{{ diagResult.system.osVersion || diagResult.system.platform }}</span>
          </div>
          <div class="diag-info-item">
            <span class="info-label">架构</span>
            <span class="info-value">{{ diagResult.system.arch }}</span>
          </div>
          <div class="diag-info-item">
            <span class="info-label">Electron</span>
            <span class="info-value">{{ diagResult.system.electron }}</span>
          </div>
          <div class="diag-info-item">
            <span class="info-label">Node</span>
            <span class="info-value">{{ diagResult.system.node }}</span>
          </div>
          <div class="diag-info-item">
            <span class="info-label">运行时间</span>
            <span class="info-value">{{ formatUptime(diagResult.system.uptime) }}</span>
          </div>
        </div>
      </div>

      <!-- 显示器 -->
      <div v-if="diagResult.system.screens?.length" class="diag-section">
        <div class="diag-section-title">显示器 ({{ diagResult.system.screens.length }} 个)</div>
        <div class="diag-screen-list">
          <div
            v-for="(s, i) in diagResult.system.screens"
            :key="i"
            class="diag-screen-item"
            :class="{ primary: s.primary }"
          >
            <span class="screen-tag">{{ s.primary ? '主屏' : `副屏${i}` }}</span>
            <span class="screen-res">{{ s.bounds.width }}x{{ s.bounds.height }}</span>
            <span class="screen-scale">{{ s.scaleFactor }}x</span>
          </div>
        </div>
      </div>

      <!-- 内存 -->
      <div class="diag-section">
        <div class="diag-section-title">内存</div>
        <div class="diag-memory-bar">
          <div class="diag-memory-fill" :style="{ width: memoryPercent + '%' }"></div>
        </div>
        <div class="diag-memory-text">
          {{ formatBytes(diagResult.system.memory.heapUsed) }} / {{ formatBytes(diagResult.system.memory.heapTotal) }}
        </div>
      </div>

      <!-- 服务状态 -->
      <div v-if="diagResult.services?.length" class="diag-section">
        <div class="diag-section-title">服务状态</div>
        <div class="diag-service-list">
          <div
            v-for="svc in diagResult.services"
            :key="svc.name"
            class="diag-service-item"
            :class="svc.status"
          >
            <span class="svc-dot" :class="svc.status"></span>
            <span class="svc-name">{{ svc.name }}</span>
            <span class="svc-msg">{{ svc.message }}</span>
          </div>
        </div>
      </div>

      <!-- 功能可用性 -->
      <div v-if="diagResult.features" class="diag-section">
        <div class="diag-section-title">功能可用性</div>
        <div class="diag-feature-list">
          <div class="diag-feature-item">
            <span class="feat-dot" :class="diagResult.features.notification ? 'ok' : 'warn'"></span>
            <span>系统通知</span>
          </div>
          <div class="diag-feature-item">
            <span class="feat-dot" :class="diagResult.features.clipboard ? 'ok' : 'warn'"></span>
            <span>剪贴板</span>
          </div>
          <div class="diag-feature-item">
            <span class="feat-dot" :class="diagResult.features.desktopCapturer ? 'ok' : 'warn'"></span>
            <span>屏幕捕获</span>
          </div>
        </div>
      </div>

      <!-- 最近错误 -->
      <div v-if="diagResult.recentErrors?.length" class="diag-section">
        <div class="diag-section-title">最近错误 ({{ diagResult.recentErrors.length }} 条)</div>
        <div class="diag-error-list">
          <div v-for="(err, i) in diagResult.recentErrors" :key="i" class="diag-error-item">
            {{ err }}
          </div>
        </div>
      </div>

      <!-- 操作按钮 -->
      <div class="diag-actions">
        <button class="btn-export" @click="exportDiagPack">导出诊断包</button>
        <button class="btn-close" @click="closeDiagnostics">关闭</button>
      </div>
    </div>

    <div class="about">
      <span class="version">v{{ diagResult?.version || '2.0.0' }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const autoLaunch = ref(true)
const diagRunning = ref(false)
const diagResult = ref<any>(null)
const memoryPercent = ref(0)

function icon(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

function openSettings() {
  window.sidekick.window.openSettings()
}

async function toggleAutoLaunch() {
  autoLaunch.value = !autoLaunch.value
  await window.sidekick.power.setAutoLaunch(autoLaunch.value)
}

async function runDiagnostics() {
  if (diagRunning.value) return
  diagRunning.value = true
  diagResult.value = null
  try {
    const result = await window.sidekick.diagnostic.runFull()
    if (result) {
      diagResult.value = result
      if (result.system?.memory?.heapTotal > 0) {
        memoryPercent.value = Math.round(
          (result.system.memory.heapUsed / result.system.memory.heapTotal) * 100
        )
      }
    }
  } catch (e) {
    console.error('Diagnostics failed:', e)
    diagResult.value = {
      timestamp: new Date().toLocaleString('zh-CN'),
      version: '2.0.0',
      system: {
        os: 'unknown', osVersion: '诊断失败', platform: '', arch: '', electron: '', chrome: '', node: '',
        uptime: 0,
        memory: { rss: 0, heapTotal: 1, heapUsed: 0, external: 0 },
        screens: []
      },
      services: [{ name: '诊断服务', status: 'error', message: '无法获取诊断信息: ' + String(e) }],
      features: { notification: true, clipboard: true, desktopCapturer: true, autoLaunch: null },
      config: {},
      recentErrors: [String(e)],
      logPath: ''
    }
  } finally {
    diagRunning.value = false
  }
}

async function exportDiagPack() {
  try {
    const path = await window.sidekick.diagnostic.exportPack()
    if (path) {
      await window.sidekick.notification.show({
        title: '诊断包已导出',
        message: path,
        duration: 5000
      })
    }
  } catch (e) {
    console.error('Export failed:', e)
    await window.sidekick.notification.show({
      title: '导出失败',
      message: String(e),
      duration: 3000
    })
  }
}

function closeDiagnostics() {
  diagResult.value = null
}

function formatUptime(sec: number): string {
  const h = Math.floor(sec / 3600)
  const m = Math.floor((sec % 3600) / 60)
  const s = Math.floor(sec % 60)
  return `${h}h ${m}m ${s}s`
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  if (bytes < 1024 * 1024 * 1024) return (bytes / 1024 / 1024).toFixed(1) + ' MB'
  return (bytes / 1024 / 1024 / 1024).toFixed(2) + ' GB'
}
</script>

<style scoped>
.panel-content { display: flex; flex-direction: column; gap: 8px; }

.setting-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  cursor: pointer;
  font-size: 13px;
  transition: all var(--anim);
}
.setting-item:hover { background: var(--brand-light); color: var(--brand); }
.setting-item.running { opacity: 0.6; cursor: wait; }
.setting-item img { width: 20px; height: 20px; }

.about { margin-top: 8px; text-align: center; }
.version { font-size: 11px; color: var(--text-secondary); }

/* ====== 诊断面板 ====== */
.diag-panel {
  background: var(--bg-hover);
  border-radius: var(--radius-md);
  border: 1px solid var(--border);
  overflow: hidden;
  animation: slideDown 200ms ease;
}
@keyframes slideDown {
  from { opacity: 0; transform: translateY(-8px); }
  to   { opacity: 1; transform: translateY(0); }
}

.diag-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background: var(--brand-light);
  border-bottom: 1px solid var(--border);
}
.diag-title { font-size: 14px; font-weight: 600; color: var(--brand); }
.diag-time  { font-size: 11px; color: var(--text-secondary); }

.diag-section {
  padding: 10px 12px;
  border-bottom: 1px solid var(--border);
}
.diag-section:last-child { border-bottom: none; }

.diag-section-title {
  font-size: 11px;
  font-weight: 600;
  color: var(--text-secondary);
  margin-bottom: 8px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

/* 系统信息网格 */
.diag-info-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 6px;
}
.diag-info-item { display: flex; flex-direction: column; gap: 2px; }
.info-label { font-size: 11px; color: var(--text-secondary); }
.info-value { font-size: 12px; color: var(--text); font-weight: 500; }

/* 显示器 */
.diag-screen-list { display: flex; flex-direction: column; gap: 4px; }
.diag-screen-item {
  display: flex; align-items: center; gap: 8px;
  padding: 6px 8px;
  background: var(--bg);
  border-radius: var(--radius-sm);
  font-size: 12px;
}
.diag-screen-item.primary {
  border: 1px solid var(--brand);
  background: var(--brand-light);
}
.screen-tag {
  font-size: 11px;
  padding: 1px 5px;
  border-radius: 4px;
  background: var(--brand);
  color: white;
  font-weight: 600;
}
.screen-res { flex: 1; color: var(--text); }
.screen-scale { font-size: 11px; color: var(--text-secondary); }

/* 内存条 */
.diag-memory-bar {
  width: 100%;
  height: 6px;
  background: var(--border);
  border-radius: 3px;
  overflow: hidden;
  margin-bottom: 4px;
}
.diag-memory-fill {
  height: 100%;
  background: var(--brand);
  border-radius: 3px;
  transition: width 300ms ease;
}
.diag-memory-text {
  font-size: 11px;
  color: var(--text-secondary);
  text-align: right;
}

/* 服务状态 */
.diag-service-list { display: flex; flex-direction: column; gap: 4px; }
.diag-service-item {
  display: flex; align-items: center; gap: 8px;
  padding: 5px 8px;
  border-radius: var(--radius-sm);
  font-size: 12px;
  background: var(--bg);
}
.svc-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
.svc-dot.ok     { background: #2ecc71; }
.svc-dot.warn   { background: #f39c12; }
.svc-dot.error  { background: #e74c3c; }
.svc-dot.unknown{ background: var(--text-disabled); }
.svc-name { font-weight: 500; width: 70px; flex-shrink: 0; }
.svc-msg {
  color: var(--text-secondary);
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 功能可用性 */
.diag-feature-list { display: flex; flex-wrap: wrap; gap: 8px; }
.diag-feature-item {
  display: flex; align-items: center; gap: 5px;
  font-size: 11px;
  color: var(--text-secondary);
}
.feat-dot { width: 7px; height: 7px; border-radius: 50%; }
.feat-dot.ok   { background: #2ecc71; }
.feat-dot.warn { background: #f39c12; }

/* 错误列表 */
.diag-error-list { display: flex; flex-direction: column; gap: 3px; }
.diag-error-item {
  font-size: 11px;
  color: #e74c3c;
  background: rgba(231, 76, 60, 0.06);
  padding: 4px 6px;
  border-radius: var(--radius-sm);
  font-family: monospace;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 操作按钮 */
.diag-actions {
  display: flex;
  gap: 8px;
  padding: 10px 12px;
  border-top: 1px solid var(--border);
}
.btn-export {
  flex: 1;
  padding: 8px;
  background: var(--brand);
  color: white;
  border-radius: var(--radius-sm);
  font-size: 13px;
  text-align: center;
  cursor: pointer;
  border: none;
}
.btn-export:hover { background: var(--brand-hover); }
.btn-close {
  padding: 8px 14px;
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  font-size: 13px;
  color: var(--text-secondary);
  cursor: pointer;
}
.btn-close:hover { background: var(--border); color: var(--text); }
</style>

<template>
  <div class="panel-content">
    <!-- 头部状态栏 -->
    <div class="usb-header">
      <div class="usb-status">
        <span class="status-label">USB 监控状态</span>
        <span class="status-badge" :class="enabled ? 'on' : 'off'">{{ enabled ? '运行中' : '已停止' }}</span>
      </div>
      <button class="refresh-btn" @click="refresh" :disabled="scanning" :class="{ spinning: scanning }">
        <svg v-if="scanning" class="spin-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M23 4v6h-6M1 20v-6h6M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15" />
        </svg>
        <span v-else>刷新</span>
      </button>
    </div>

    <!-- 扫描中提示 -->
    <div v-if="scanning" class="scan-hint">
      <span class="pulse-dot"></span>
      <span>正在扫描 USB 设备...</span>
    </div>

    <!-- 错误提示 -->
    <div v-if="error" class="error-hint">
      <span class="error-icon">!</span>
      <span>{{ error }}</span>
    </div>

    <!-- 设备列表 -->
    <div class="usb-list" v-if="drives.length > 0">
      <h4>已连接设备 ({{ drives.length }})</h4>
      <div class="drive-item" v-for="d in drives" :key="d.drive">
        <img :src="icon('usb')" class="drive-icon" />
        <div class="drive-info">
          <div class="drive-header">
            <span class="drive-letter">{{ d.drive }}</span>
            <span class="drive-type" :class="d.type">{{ typeLabel(d.type) }}</span>
          </div>
          <span class="drive-label">{{ d.label || d.model || 'USB 存储设备' }}</span>
          <span class="drive-size" v-if="d.size">{{ d.size }}{{ d.model ? ' · ' + d.model : '' }}</span>
        </div>
        <button class="open-btn" @click="openDrive(d.drive)" title="在资源管理器中打开">
          <span>打开</span>
        </button>
      </div>
    </div>

    <div v-else class="usb-empty">
      <img :src="icon('usb')" class="empty-icon" />
      <p>暂无 USB 设备连接</p>
      <p class="empty-hint">插入 U 盘后将自动显示，或点击刷新手动检测</p>
    </div>

    <!-- 最近事件 -->
    <div class="event-log" v-if="events.length > 0">
      <h4>最近事件</h4>
      <div class="event-item" v-for="(e, i) in events" :key="i">
        <span class="event-icon" :class="e.action">{{ e.action === 'arrived' ? '+' : '-' }}</span>
        <span class="event-drive">{{ e.drive }}</span>
        <span class="event-time">{{ e.time }}</span>
      </div>
    </div>

    <!-- 诊断信息 (折叠) -->
    <details class="diag-section" v-if="diag">
      <summary>诊断信息</summary>
      <pre class="diag-content">{{ JSON.stringify(diag, null, 2) }}</pre>
    </details>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

const drives = ref<Array<{drive: string; label?: string; type?: string; size?: string; model?: string}>>([])
const enabled = ref(true)
const scanning = ref(false)
const error = ref('')
const events = ref<Array<{action: string; drive: string; time: string}>>([])
const diag = ref<any>(null)
let unsubArrived: (() => void) | null = null
let unsubRemoved: (() => void) | null = null

function icon(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

function typeLabel(type?: string): string {
  if (type === 'cdrom') return '光驱'
  if (type === 'external') return '移动硬盘'
  if (type === 'usb') return 'USB'
  return 'U盘'
}

function formatTime(): string {
  return new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
}

async function loadDevices() {
  try {
    const list = await window.sidekick.usb.list()
    drives.value = list || []
  } catch (e) {
    console.error('[UsbPanel] loadDevices failed:', e)
  }
}

async function refresh() {
  if (scanning.value) return
  scanning.value = true
  error.value = ''
  try {
    const result = await window.sidekick.usb.scan()
    if (result && result.drives) {
      drives.value = result.drives
      if (result.drives.length === 0) {
        // 仍尝试从 list 获取
        await loadDevices()
      }
    } else {
      await loadDevices()
    }
  } catch (e: any) {
    error.value = '扫描失败: ' + (e.message || '未知错误')
    console.error('[UsbPanel] refresh error:', e)
  } finally {
    scanning.value = false
  }
}

async function loadDiagnostics() {
  try {
    diag.value = await window.sidekick.usb.getDiagnostics()
  } catch { /* ignore */ }
}

onMounted(async () => {
  try {
    const cfg = await window.sidekick.config.get()
    enabled.value = cfg.usb.enabled
  } catch { /* degraded */ }

  await loadDevices()
  await loadDiagnostics()

  // 监听插入
  unsubArrived = window.sidekick.usb.onArrived((d: any) => {
    if (!drives.value.find(x => x.drive === d.drive)) {
      drives.value.push({ drive: d.drive, label: d.label, type: d.type, size: d.size, model: d.model })
    }
    events.value.unshift({ action: 'arrived', drive: d.drive, time: formatTime() })
    if (events.value.length > 5) events.value.pop()
  })

  // 监听拔出
  unsubRemoved = window.sidekick.usb.onRemoved((d: any) => {
    drives.value = drives.value.filter(x => x.drive !== d.drive)
    events.value.unshift({ action: 'removed', drive: d.drive, time: formatTime() })
    if (events.value.length > 5) events.value.pop()
  })
})

onUnmounted(() => {
  if (unsubArrived) unsubArrived()
  if (unsubRemoved) unsubRemoved()
})

async function openDrive(drive: string) {
  try {
    await window.sidekick.shell.openPath(drive + '\\')
  } catch (e) { console.error(e) }
}
</script>

<style scoped>
.panel-content { display: flex; flex-direction: column; gap: 16px; }

/* 头部 */
.usb-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: var(--bg-hover);
  border-radius: var(--radius-md);
}

.usb-status {
  display: flex;
  align-items: center;
  gap: 8px;
}

.status-label { font-size: 13px; }
.status-badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; color: white; }
.status-badge.on { background: var(--success); }
.status-badge.off { background: var(--text-disabled); }

.refresh-btn {
  padding: 4px 12px;
  border-radius: var(--radius-sm);
  font-size: 12px;
  background: var(--brand-light);
  color: var(--brand);
  border: 1px solid var(--brand);
  min-width: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
}
.refresh-btn:hover:not(:disabled) { background: var(--brand); color: white; }
.refresh-btn:disabled { opacity: 0.6; cursor: not-allowed; }
.refresh-btn.spinning { background: var(--brand); color: white; }

.spin-icon {
  width: 16px;
  height: 16px;
  animation: spin 1s linear infinite;
}
@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* 扫描中 */
.scan-hint {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: rgba(43, 110, 224, 0.08);
  border-radius: var(--radius-sm);
  font-size: 12px;
  color: var(--brand);
}

.pulse-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--brand);
  animation: pulse 1.2s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.4; transform: scale(0.8); }
}

/* 错误 */
.error-hint {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: rgba(231, 76, 60, 0.08);
  border-radius: var(--radius-sm);
  font-size: 12px;
  color: var(--danger);
}

.error-icon {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: var(--danger);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: bold;
  flex-shrink: 0;
}

/* 设备列表 */
.usb-list h4 { font-size: 13px; margin-bottom: 8px; color: var(--text-secondary); }

.drive-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  margin-bottom: 6px;
}

.drive-icon { width: 20px; height: 20px; flex-shrink: 0; }

.drive-info { flex: 1; display: flex; flex-direction: column; gap: 2px; }

.drive-header { display: flex; align-items: center; gap: 6px; }

.drive-letter { font-size: 15px; font-weight: 600; }

.drive-type {
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 11px;
  color: #fff;
  background: var(--brand);
}
.drive-type.cdrom { background: #8e44ad; }
.drive-type.usb { background: #27ae60; }
.drive-type.external { background: #d35400; }

.drive-label { font-size: 12px; color: var(--text-secondary); }
.drive-size { font-size: 11px; color: var(--text-disabled); }

.open-btn {
  padding: 4px 12px;
  background: var(--brand);
  color: #fff;
  border-radius: 6px;
  font-size: 12px;
}
.open-btn:hover { background: var(--brand-hover); }

/* 空状态 */
.usb-empty {
  text-align: center;
  padding: 32px 16px;
  color: var(--text-secondary);
}

.empty-icon { width: 48px; height: 48px; opacity: 0.25; margin-bottom: 8px; }
.empty-hint { font-size: 11px; color: var(--text-disabled); margin-top: 4px; }

/* 事件日志 */
.event-log h4 { font-size: 12px; margin-bottom: 6px; color: var(--text-secondary); }

.event-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  font-size: 12px;
}

.event-icon {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: bold;
  color: #fff;
}

.event-icon.arrived { background: var(--success); }
.event-icon.removed { background: var(--danger); }

.event-drive { font-weight: 500; }
.event-time { margin-left: auto; color: var(--text-disabled); font-size: 11px; }

/* 诊断 */
.diag-section {
  margin-top: 8px;
  font-size: 11px;
}
.diag-section summary {
  cursor: pointer;
  color: var(--text-secondary);
  padding: 4px 0;
}
.diag-content {
  background: var(--bg-hover);
  padding: 8px;
  border-radius: var(--radius-sm);
  font-family: monospace;
  font-size: 11px;
  max-height: 200px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>

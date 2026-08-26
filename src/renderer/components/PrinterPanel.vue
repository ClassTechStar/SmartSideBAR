<template>
  <div class="panel-content">
    <div class="printer-list" v-if="printers.length > 0">
      <div class="printer-item" v-for="p in printers" :key="p.name">
        <img :src="icon('printer')" class="printer-icon" />
        <div class="printer-info">
          <span class="printer-name">{{ p.name }}</span>
          <span class="printer-state" :class="p.state">{{ stateText(p.state) }}</span>
        </div>
      </div>
    </div>
    <div v-else class="printer-empty">
      <img :src="icon('printer')" class="empty-icon" />
      <p>未检测到打印机</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import type { PrinterStatus } from '../../shared/types'

const printers = ref<PrinterStatus[]>([])
let unsub: (() => void) | null = null

function icon(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

function stateText(state: string): string {
  const map: Record<string, string> = {
    ok: '正常',
    out_of_paper: '缺纸',
    jammed: '卡纸',
    offline: '离线',
    low_ink: '墨量低',
    unknown: '未知'
  }
  return map[state] || '未知'
}

onMounted(async () => {
  try {
    const list = await window.sidekick.printer.getStatus()
    printers.value = list || []
  } catch { /* degraded */ }

  unsub = window.sidekick.printer.onChanged((status: PrinterStatus) => {
    const idx = printers.value.findIndex(p => p.name === status.name)
    if (idx >= 0) {
      printers.value[idx] = status
    } else {
      printers.value.push(status)
    }
  })
})

onUnmounted(() => { if (unsub) unsub() })
</script>

<style scoped>
.panel-content { display: flex; flex-direction: column; gap: 12px; }

.printer-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
}

.printer-icon { width: 20px; height: 20px; }
.printer-info { display: flex; flex-direction: column; }
.printer-name { font-size: 13px; font-weight: 500; }

.printer-state { font-size: 12px; }
.printer-state.ok { color: var(--success); }
.printer-state.out_of_paper, .printer-state.jammed, .printer-state.offline { color: var(--danger); }
.printer-state.low_ink { color: var(--warn); }
.printer-state.unknown { color: var(--text-secondary); }

.printer-empty { text-align: center; padding: 32px; color: var(--text-secondary); }
.empty-icon { width: 40px; height: 40px; opacity: 0.3; margin-bottom: 8px; }
</style>

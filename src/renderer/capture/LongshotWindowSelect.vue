<template>
  <div class="longshot-window-select">
    <div class="select-header">
      <img :src="icon('longshot')" />
      <span>选择要截图的窗口</span>
    </div>
    <div class="window-list">
      <div v-for="w in windows" :key="w.handle" class="window-item" @click="emit('confirm', w)">
        <div class="window-title">{{ w.title }}</div>
        <div class="window-meta">{{ w.name }} · PID {{ w.pid }} · {{ w.width }}x{{ w.height }}</div>
      </div>
      <div v-if="windows.length === 0" class="window-empty">
        <p>未检测到可见窗口</p>
        <p class="hint">请确保目标窗口未被最小化</p>
      </div>
    </div>
    <button class="cancel-btn" @click="emit('cancel')">取消</button>
  </div>
</template>

<script setup lang="ts">
import type { WindowInfo } from '../composables/useCaptureActions'

defineProps<{ windows: WindowInfo[] }>()

const emit = defineEmits<{
  (e: 'confirm', w: WindowInfo): void
  (e: 'cancel'): void
}>()

function icon(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}
</script>

<style scoped>
.longshot-window-select {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.select-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 600;
  color: var(--brand);
}

.select-header img { width: 20px; height: 20px; }

.window-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-height: 280px;
  overflow-y: auto;
}

.window-item {
  padding: 10px 12px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  cursor: pointer;
  border: 1px solid transparent;
  transition: all var(--anim);
}

.window-item:hover {
  border-color: var(--brand);
  background: var(--brand-light);
}

.window-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.window-meta {
  font-size: 11px;
  color: var(--text-secondary);
  margin-top: 2px;
}

.window-empty {
  text-align: center;
  padding: 24px 0;
  color: var(--text-secondary);
}

.window-empty .hint {
  font-size: 11px;
  margin-top: 4px;
}

.cancel-btn {
  padding: 8px 16px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  font-size: 13px;
  color: var(--text-secondary);
  text-align: center;
}

.cancel-btn:hover { color: var(--text); background: var(--border); }
</style>

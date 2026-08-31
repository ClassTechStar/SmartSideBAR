<template>
  <div class="recent-section" v-if="files.length > 0">
    <h4>最近文件</h4>
    <div class="file-list">
      <div class="file-item" v-for="f in files" :key="f.path" @click="emit('open', f.path)">
        <span class="file-type" :class="f.type">{{ f.type === 'video' ? '录屏' : '截图' }}</span>
        <span class="file-name">{{ f.name }}</span>
        <span class="file-time">{{ f.time }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { RecentFile } from '../composables/useCaptureActions'

defineProps<{ files: RecentFile[] }>()
const emit = defineEmits<{ (e: 'open', path: string): void }>()
</script>

<style scoped>
.recent-section h4 {
  font-size: 13px;
  margin-bottom: 8px;
  color: var(--text-secondary);
}

.file-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.file-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  cursor: pointer;
}

.file-item:hover { background: var(--brand-light); }

.file-type {
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 11px;
  color: #fff;
}

.file-type.image { background: var(--brand); }
.file-type.video { background: #e74c3c; }

.file-name {
  flex: 1;
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-time {
  font-size: 11px;
  color: var(--text-disabled);
}
</style>

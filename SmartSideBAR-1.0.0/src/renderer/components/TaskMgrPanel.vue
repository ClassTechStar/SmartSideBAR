<template>
  <div class="panel-content">
    <p class="hint">点击下方按钮打开 Windows 任务管理器</p>
    <button class="btn-taskmgr" @click="openTaskMgr">
      <img :src="icon('taskmgr')" />
      <span>打开任务管理器</span>
    </button>
  </div>
</template>

<script setup lang="ts">
function icon(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

async function openTaskMgr() {
  try {
    await window.sidekick.app.openTaskMgr()
  } catch (e) { console.error(e) }
}
</script>

<style scoped>
.panel-content { display: flex; flex-direction: column; gap: 16px; align-items: center; padding: 20px 0; }

.hint { font-size: 12px; color: var(--text-secondary); text-align: center; }

.btn-taskmgr {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 20px 32px;
  background: var(--bg-hover);
  border-radius: var(--radius-md);
  font-size: 14px;
  color: var(--text);
}

.btn-taskmgr:hover { background: var(--brand-light); color: var(--brand); }
.btn-taskmgr img { width: 32px; height: 32px; }
</style>

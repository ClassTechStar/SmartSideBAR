<template>
  <div class="panel-content">
    <div class="ime-current">
      <div class="ime-indicator" :class="imeMode">{{ imeMode === 'cn' ? '中' : 'EN' }}</div>
      <div class="ime-info">
        <span class="ime-label">当前输入法</span>
        <span class="ime-value">{{ imeLocale }}</span>
      </div>
    </div>
    <button class="btn-toggle" @click="toggle">切换输入法</button>
    <div class="ime-hint">点击按钮或侧边栏图标可快速切换中英文输入法</div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

const imeMode = ref<'cn' | 'en'>('cn')
const imeLocale = ref('zh-CN')
let unsub: (() => void) | null = null

async function loadState() {
  try {
    const s = await window.sidekick.ime.getState()
    imeMode.value = s.mode
    imeLocale.value = s.locale
  } catch { /* degraded */ }
}

async function toggle() {
  try {
    const s = await window.sidekick.ime.toggle()
    imeMode.value = s.mode
    imeLocale.value = s.locale
  } catch (e) {
    console.error(e)
  }
}

onMounted(() => {
  loadState()
  unsub = window.sidekick.ime.onChanged((s: any) => {
    imeMode.value = s.mode
    imeLocale.value = s.locale
  })
})

onUnmounted(() => { if (unsub) unsub() })
</script>

<style scoped>
.panel-content { display: flex; flex-direction: column; gap: 16px; }

.ime-current {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
  background: var(--bg-hover);
  border-radius: var(--radius-md);
}

.ime-indicator {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  font-weight: 700;
  color: white;
}

.ime-indicator.cn { background: var(--brand); }
.ime-indicator.en { background: var(--text-secondary); }

.ime-info { display: flex; flex-direction: column; gap: 2px; }
.ime-label { font-size: 12px; color: var(--text-secondary); }
.ime-value { font-size: 15px; font-weight: 500; }

.btn-toggle {
  padding: 10px;
  background: var(--brand);
  color: white;
  border-radius: var(--radius-sm);
  font-size: 14px;
  font-weight: 500;
}

.btn-toggle:hover { background: var(--brand-hover); }

.ime-hint { font-size: 12px; color: var(--text-secondary); text-align: center; }
</style>

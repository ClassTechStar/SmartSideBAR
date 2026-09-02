<template>
  <LiquidGlassDefs :lens-map="lensMap" />
  <router-view />
</template>

<script setup lang="ts">
// 根组件: 承载路由 + 注入液态玻璃 CSS 变量 (从 v1.1 编译产物重建)
import { ref, onMounted, onUnmounted } from 'vue'
import LiquidGlassDefs from './glass/LiquidGlassDefs.vue'
import { glassCssVars } from '@shared/appearance'
import { makeLensMap } from './composables/useGlass'

const lensMap = ref('')

function applyAppearance(snap: any): void {
  const el = document.documentElement
  const vars = glassCssVars(snap.appearance, snap.theme, snap.effectiveMaterial)
  for (const [k, v] of Object.entries(vars)) {
    el.style.setProperty(k, v as string)
  }
  el.setAttribute('data-theme', snap.theme)
  el.setAttribute('data-glass', snap.appearance.liquidGlass ? 'on' : 'off')
  el.setAttribute('data-motion', snap.appearance.reduceMotion ? 'reduced' : 'full')
}

let unsub: (() => void) | null = null
onMounted(async () => {
  lensMap.value = makeLensMap(128)
  try {
    const snap = await window.sidekick.appearance.get()
    applyAppearance(snap)
    unsub = window.sidekick.appearance.onChanged(applyAppearance)
  } catch (e) {
    console.warn('[App] 外观初始化失败（将使用兜底 token）:', e)
  }
})
onUnmounted(() => {
  unsub?.()
})
</script>

<style>
:root {
  --brand: #2B6EE0;
  --brand-hover: #1E5BC0;
  --brand-light: #E8F1FC;
  --bg: #ffffff;
  --bg-hover: #f5f7fa;
  --text: #1a1a1a;
  --text-secondary: #666666;
  --text-disabled: #999999;
  --border: #e8e8e8;
  --danger: #e74c3c;
  --warn: #f39c12;
  --success: #27ae60;
  --radius-sm: 6px;
  --radius-md: 8px;
  --radius-lg: 10px;
  --anim: 120ms;
}

* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: 'Microsoft YaHei UI', 'Segoe UI', system-ui, -apple-system, sans-serif;
  font-size: 13px;
  color: var(--text);
  background: transparent;
  -webkit-font-smoothing: antialiased;
  user-select: none;
}

button {
  font-family: inherit;
  cursor: pointer;
  border: none;
  outline: none;
  transition: all var(--anim) ease;
}

::-webkit-scrollbar { width: 4px; }
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb { background: var(--border); border-radius: 2px; }
</style>

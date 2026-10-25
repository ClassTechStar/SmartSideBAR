<template>
  <router-view />
</template>

<script setup lang="ts">
// 根组件: 仅承载路由
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

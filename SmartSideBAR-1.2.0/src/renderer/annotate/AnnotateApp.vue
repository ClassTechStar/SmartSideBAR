<template>
  <div class="annotate-root" :class="{ 'with-bg': bgOpacity > 0 }" :style="rootStyle">
    <!-- 透明模式指示器 -->
    <div v-if="isTransparent" class="transparent-indicator">
      <span class="indicator-dot"></span>
      <span class="indicator-text">透明绘图模式</span>
    </div>

    <!-- 背景遮罩层 (可选, 方便在复杂背景上绘图) -->
    <div v-if="bgOpacity > 0" class="bg-mask" :style="{ opacity: bgOpacity / 100 }"></div>

    <!-- 绘图画布 (P0-5: Pointer Events 统一鼠标/触摸/笔输入) -->
    <canvas
      ref="canvasRef"
      :width="canvasWidth"
      :height="canvasHeight"
      class="draw-canvas"
      :style="{ width: dipWidth + 'px', height: dipHeight + 'px', cursor: cursorStyle }"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
      @pointercancel="onPointerUp"
      @contextmenu.prevent
    />

    <!-- 工具栏 (P2-10: 抽为子组件) -->
    <AnnotateToolbar
      v-if="ready"
      :current-tool="currentTool"
      :current-color="currentColor"
      :current-size="currentSize"
      :bg-opacity="bgOpacity"
      :is-transparent="isTransparent"
      :can-undo="canUndo"
      :has-background="hasBackground"
      @update:tool="currentTool = $event"
      @update:color="currentColor = $event"
      @update:size="currentSize = $event"
      @update:bg-opacity="bgOpacity = $event"
      @toggle-bg="toggleBg"
      @undo="undo"
      @save-only="saveAnnotationsOnly"
      @save-bg="saveWithBackground"
      @cancel="cancel"
    />

    <!-- 文字输入框 -->
    <input
      v-if="textEditing"
      ref="textInputRef"
      v-model="textInput"
      class="text-input"
      :style="textInputStyle"
      @blur="commitText"
      @keydown.enter="commitText"
      @keydown.escape="cancelText"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import AnnotateToolbar from './AnnotateToolbar.vue'
import { useAnnotate } from '../composables/useAnnotate'

const {
  canvasRef, textInputRef,
  ready, isTransparent, bgOpacity, dipWidth, dipHeight, canvasWidth, canvasHeight,
  hasBackground, currentTool, currentColor, currentSize, canUndo,
  textEditing, textInput, textInputStyle,
  cursorStyle,
  setupCanvas, setBackground, toggleBg,
  onPointerDown, onPointerMove, onPointerUp,
  commitText, cancelText, undo, saveAnnotationsOnly, saveWithBackground
} = useAnnotate()

const rootStyle = computed(() => ({
  width: dipWidth.value + 'px',
  height: dipHeight.value + 'px',
  backgroundColor: isTransparent.value ? 'transparent' : '#000'
}))

function cancel() {
  window.sidekick.overlay.cancel()
}

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    if (textEditing.value) cancelText()
    else cancel()
  }
  if (e.ctrlKey && e.key === 'z') {
    e.preventDefault()
    undo()
  }
}

let unsubInit: (() => void) | null = null
let unsubScreenshot: (() => void) | null = null

onMounted(() => {
  window.sidekick.overlay.ready()

  unsubInit = window.sidekick.overlay.onInit((init: any) => {
    if (init.mode !== 'annotate') return
    const transparent = !!init.transparent || !!init.screenshotDataUrl
    setupCanvas(
      init.dipWidth || window.innerWidth,
      init.dipHeight || window.innerHeight,
      init.scaleFactor || 1,
      transparent
    )
  })

  document.addEventListener('keydown', onKey)

  // P1-8: 接收主进程异步推送的屏幕截图, 供「含截图」导出使用
  unsubScreenshot = window.sidekick.overlay.onScreenshot((dataUrl: string) => setBackground(dataUrl))
})

onUnmounted(() => {
  if (unsubInit) unsubInit()
  if (unsubScreenshot) unsubScreenshot()
  document.removeEventListener('keydown', onKey)
})
</script>

<style scoped>
.annotate-root {
  position: fixed;
  top: 0;
  left: 0;
  overflow: hidden;
  background: transparent;
}

.annotate-root.with-bg { background: #f0f0f0; }

/* 透明模式指示器 */
.transparent-indicator {
  position: fixed;
  top: 16px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 14px;
  background: rgba(30, 30, 30, 0.85);
  border-radius: 20px;
  color: #fff;
  font-size: 13px;
  z-index: 10;
  pointer-events: none;
  user-select: none;
  backdrop-filter: blur(4px);
  animation: fadeInDown 0.4s ease;
}

@keyframes fadeInDown {
  from { opacity: 0; transform: translateX(-50%) translateY(-10px); }
  to { opacity: 1; transform: translateX(-50%) translateY(0); }
}

.indicator-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #27ae60;
  animation: pulse 2s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

.indicator-text { font-weight: 500; }

/* 背景遮罩层 */
.bg-mask {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: #fff;
  z-index: 0;
  pointer-events: none;
}

.draw-canvas {
  position: absolute;
  top: 0;
  left: 0;
  z-index: 2;
  background: transparent;
  /* P0-5: 触摸书写禁用浏览器手势 (滚动/双指缩放) */
  touch-action: none;
}

.text-input {
  position: fixed;
  z-index: 5;
  background: rgba(255, 255, 255, 0.9);
  border: 2px solid var(--brand, #2B6EE0);
  border-radius: 4px;
  padding: 4px 8px;
  outline: none;
  font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif;
  min-width: 100px;
}
</style>

<template>
  <div class="overlay-root"
    @mousedown="onMouseDown" @mousemove="onMouseMove" @mouseup="onMouseUp"
    @touchstart="onTouchStart" @touchmove="onTouchMove" @touchend="onTouchEnd">
    <!-- 遮罩层 -->
    <div class="overlay-mask"></div>
    
    <!-- 选区框 -->
    <div v-if="isSelecting || hasSelected" class="selection-box" :style="selectionStyle">
      <!-- 选区四角装饰 -->
      <div class="corner tl"></div>
      <div class="corner tr"></div>
      <div class="corner bl"></div>
      <div class="corner br"></div>
      
      <!-- 尺寸标签 -->
      <div v-if="isSelecting" class="size-label">
        {{ Math.round(selWidth) }} x {{ Math.round(selHeight) }}
      </div>
    </div>
    
    <!-- 操作栏 (选区完成后显示) -->
    <div v-if="hasSelected && !isSelecting" class="action-bar" :style="actionBarStyle">
      <button class="btn confirm" @click.stop="confirmSelection">确认截图</button>
      <button class="btn redo" @click.stop="resetSelection">重新选择</button>
      <button class="btn cancel" @click.stop="cancelSelection">取消</button>
    </div>
    
    <!-- 顶部提示 -->
    <div class="hint-bar" v-if="!hasSelected">
      <span class="hint-text">拖拽选择截图区域</span>
      <span class="hint-key">ESC 取消</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'

const isSelecting = ref(false)
const hasSelected = ref(false)
const startX = ref(0)
const startY = ref(0)
const endX = ref(0)
const endY = ref(0)

// MLP 1.11-⑦: 触摸框选抖动过滤 (4px / 80ms)
const JITTER_PX = 4
const JITTER_MS = 80
let lastMoveX = 0
let lastMoveY = 0
let lastMoveTime = 0

const selLeft = computed(() => Math.min(startX.value, endX.value))
const selTop = computed(() => Math.min(startY.value, endY.value))
const selWidth = computed(() => Math.abs(endX.value - startX.value))
const selHeight = computed(() => Math.abs(endY.value - startY.value))

const selectionStyle = computed(() => ({
  left: selLeft.value + 'px',
  top: selTop.value + 'px',
  width: selWidth.value + 'px',
  height: selHeight.value + 'px'
}))

const actionBarStyle = computed(() => {
  let top = selTop.value + selHeight.value + 8
  if (top + 40 > window.innerHeight) {
    top = selTop.value - 44
  }
  let left = selLeft.value + selWidth.value / 2
  left = Math.max(120, Math.min(left, window.innerWidth - 120))
  return {
    left: left + 'px',
    top: top + 'px',
    transform: 'translateX(-50%)'
  }
})

let unsubInit: (() => void) | null = null

onMounted(() => {
  window.sidekick.overlay.ready()

  unsubInit = window.sidekick.overlay.onInit((_init: any) => {
    // 区域截图模式, 坐标使用 DIP 发送
  })

  document.addEventListener('keydown', onKey)
  document.addEventListener('contextmenu', preventDefault)
})

onUnmounted(() => {
  if (unsubInit) unsubInit()
  document.removeEventListener('keydown', onKey)
  document.removeEventListener('contextmenu', preventDefault)
})

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    cancelSelection()
  }
}

function preventDefault(e: Event) {
  e.preventDefault()
}

// --- 鼠标事件 ---
function onMouseDown(e: MouseEvent) {
  if (hasSelected.value && !isInSelection(e.clientX, e.clientY)) {
    resetSelection()
  }
  if (hasSelected.value) return
  beginSelection(e.clientX, e.clientY)
}

function onMouseMove(e: MouseEvent) {
  if (!isSelecting.value) return
  updateSelection(e.clientX, e.clientY)
}

function onMouseUp(_e: MouseEvent) {
  finishSelection()
}

// --- 触摸事件 (希沃大屏触控) ---
function onTouchStart(e: TouchEvent) {
  if (e.touches.length !== 1) return
  e.preventDefault()
  const t = e.touches[0]
  if (hasSelected.value && !isInSelection(t.clientX, t.clientY)) {
    resetSelection()
  }
  if (hasSelected.value) return
  beginSelection(t.clientX, t.clientY)
}

function onTouchMove(e: TouchEvent) {
  if (!isSelecting.value || e.touches.length !== 1) return
  e.preventDefault()
  const t = e.touches[0]
  updateSelection(t.clientX, t.clientY)
}

function onTouchEnd(_e: TouchEvent) {
  finishSelection()
}

// --- 选区核心逻辑 ---
function beginSelection(x: number, y: number) {
  isSelecting.value = true
  startX.value = x
  startY.value = y
  endX.value = x
  endY.value = y
  lastMoveX = x
  lastMoveY = y
  lastMoveTime = Date.now()
}

function updateSelection(x: number, y: number) {
  // MLP 1.11-⑦: 抖动过滤 — 移动距离 <4px 或时间间隔 <80ms 则忽略
  const now = Date.now()
  const dx = Math.abs(x - lastMoveX)
  const dy = Math.abs(y - lastMoveY)
  const dt = now - lastMoveTime
  if (dx < JITTER_PX && dy < JITTER_PX && dt < JITTER_MS) {
    return // 抖动, 忽略
  }
  endX.value = x
  endY.value = y
  lastMoveX = x
  lastMoveY = y
  lastMoveTime = now
}

function finishSelection() {
  if (!isSelecting.value) return
  isSelecting.value = false
  if (selWidth.value > 5 && selHeight.value > 5) {
    hasSelected.value = true
  } else {
    hasSelected.value = false
  }
}

function isInSelection(x: number, y: number): boolean {
  return x >= selLeft.value && 
         x <= selLeft.value + selWidth.value &&
         y >= selTop.value && 
         y <= selTop.value + selHeight.value
}

function confirmSelection() {
  window.sidekick.overlay.sendRegion({
    x: selLeft.value,
    y: selTop.value,
    width: selWidth.value,
    height: selHeight.value
  })
}

function resetSelection() {
  hasSelected.value = false
  isSelecting.value = false
  startX.value = 0
  startY.value = 0
  endX.value = 0
  endY.value = 0
}

function cancelSelection() {
  window.sidekick.overlay.cancel()
}
</script>

<style scoped>
.overlay-root {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  cursor: crosshair;
  z-index: 99999;
  touch-action: none;
}

.overlay-mask {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(0, 0, 0, 0.35);
  pointer-events: none;
}

.selection-box {
  position: absolute;
  border: 2px solid #2B6EE0;
  background: rgba(43, 110, 224, 0.08);
  pointer-events: none;
  box-sizing: border-box;
}

.corner {
  position: absolute;
  width: 12px;
  height: 12px;
  border: 3px solid #2B6EE0;
  background: #fff;
}

.corner.tl { top: -7px; left: -7px; border-right: none; border-bottom: none; }
.corner.tr { top: -7px; right: -7px; border-left: none; border-bottom: none; }
.corner.bl { bottom: -7px; left: -7px; border-right: none; border-top: none; }
.corner.br { bottom: -7px; right: -7px; border-left: none; border-top: none; }

.size-label {
  position: absolute;
  top: -28px;
  left: 0;
  padding: 2px 8px;
  background: #2B6EE0;
  color: #fff;
  font-size: 12px;
  border-radius: 4px;
  white-space: nowrap;
}

.action-bar {
  position: fixed;
  display: flex;
  gap: 4px;
  padding: 6px;
  background: rgba(30, 30, 30, 0.92);
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
  z-index: 100000;
}

.btn {
  padding: 6px 14px;
  border-radius: 6px;
  font-size: 13px;
  color: #ccc;
  background: transparent;
  border: none;
  cursor: pointer;
  transition: all 120ms ease;
}

.btn:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}

.btn.confirm {
  background: #27ae60;
  color: #fff;
}

.btn.confirm:hover {
  background: #229954;
}

.btn.cancel {
  background: rgba(231, 76, 60, 0.8);
  color: #fff;
}

.btn.cancel:hover {
  background: #c0392b;
}

.hint-bar {
  position: fixed;
  top: 20px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 8px 20px;
  background: rgba(0, 0, 0, 0.7);
  border-radius: 24px;
  color: #fff;
  font-size: 14px;
  pointer-events: none;
  z-index: 100000;
}

.hint-key {
  padding: 2px 8px;
  background: rgba(255, 255, 255, 0.15);
  border-radius: 4px;
  font-size: 12px;
}
</style>

<template>
  <div class="annotate-root" :class="{ 'with-bg': bgOpacity > 0 }" :style="rootStyle">
    <!-- 透明模式指示器 -->
    <div v-if="isTransparent" class="transparent-indicator">
      <span class="indicator-dot"></span>
      <span class="indicator-text">透明绘图模式</span>
    </div>

    <!-- 背景遮罩层 (可选, 方便在复杂背景上绘图) -->
    <div v-if="bgOpacity > 0" class="bg-mask" :style="{ opacity: bgOpacity / 100 }"></div>

    <!-- 绘图画布 -->
    <canvas
      ref="canvasRef"
      :width="canvasWidth"
      :height="canvasHeight"
      class="draw-canvas"
      :style="{ width: dipWidth + 'px', height: dipHeight + 'px', cursor: cursorStyle }"
      @mousedown="onMouseDown"
      @mousemove="onMouseMove"
      @mouseup="onMouseUp"
    />

    <!-- 工具栏 -->
    <div class="toolbar" v-if="ready">
      <div class="tool-group">
        <button
          v-for="t in tools"
          :key="t.id"
          class="tool-btn"
          :class="{ active: currentTool === t.id }"
          @click="currentTool = t.id"
          :title="t.label"
        >
          <span class="tool-icon">{{ t.icon }}</span>
        </button>
      </div>

      <div class="divider"></div>

      <div class="color-group">
        <button
          v-for="c in colors"
          :key="c"
          class="color-btn"
          :class="{ active: currentColor === c }"
          :style="{ background: c }"
          @click="currentColor = c"
        />
      </div>

      <div class="divider"></div>

      <div class="size-group">
        <button
          v-for="s in sizes"
          :key="s.value"
          class="size-btn"
          :class="{ active: currentSize === s.value }"
          @click="currentSize = s.value"
          :title="s.label"
        >
          <span class="size-dot" :style="{ width: s.value + 'px', height: s.value + 'px' }"></span>
        </button>
      </div>

      <div class="divider"></div>

      <!-- 背景遮罩滑块 (透明模式下显示) -->
      <div v-if="isTransparent" class="bg-control">
        <button
          class="bg-toggle"
          :class="{ active: bgOpacity > 0 }"
          @click="toggleBg"
          :title="bgOpacity > 0 ? '关闭背景遮罩' : '开启背景遮罩'"
        >
          <span>{{ bgOpacity > 0 ? '遮罩开' : '遮罩关' }}</span>
        </button>
        <input
          v-if="bgOpacity > 0"
          type="range"
          class="bg-slider"
          min="0"
          max="80"
          v-model.number="bgOpacity"
        />
      </div>

      <div class="divider" v-if="isTransparent"></div>

      <button class="action-btn undo" @click="undo" :disabled="undoStack.length === 0">
        <span>撤销</span>
      </button>
      <button class="action-btn save" @click="save">
        <span>保存</span>
      </button>
      <button class="action-btn cancel" @click="cancel">
        <span>取消</span>
      </button>
    </div>

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
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'

const canvasRef = ref<HTMLCanvasElement | null>(null)
const textInputRef = ref<HTMLInputElement | null>(null)

const ready = ref(false)
const isTransparent = ref(false)
const bgOpacity = ref(0)
const dipWidth = ref(window.innerWidth)
const dipHeight = ref(window.innerHeight)
const canvasWidth = ref(0)
const canvasHeight = ref(0)
const scaleFactor = ref(1)

const currentTool = ref<'pen' | 'highlighter' | 'arrow' | 'rect' | 'text' | 'eraser'>('pen')
const currentColor = ref('#e74c3c')
const currentSize = ref(3)

const isDrawing = ref(false)
const startX = ref(0)
const startY = ref(0)
let savedImageData: ImageData | null = null

const undoStack: ImageData[] = []
const MAX_UNDO = 30

// 文字编辑
const textEditing = ref(false)
const textInput = ref('')
const textX = ref(0)
const textY = ref(0)
const textInputStyle = ref<Record<string, string>>({})

const tools = [
  { id: 'pen' as const, label: '画笔', icon: '✏️' },
  { id: 'highlighter' as const, label: '高亮笔', icon: '🖊️' },
  { id: 'arrow' as const, label: '箭头', icon: '➤' },
  { id: 'rect' as const, label: '矩形', icon: '▭' },
  { id: 'text' as const, label: '文字', icon: 'T' },
  { id: 'eraser' as const, label: '橡皮擦', icon: '🧹' }
]

const colors = ['#e74c3c', '#2B6EE0', '#27ae60', '#f39c12', '#ffffff', '#000000']
const sizes = [
  { value: 2, label: '细' },
  { value: 4, label: '中' },
  { value: 8, label: '粗' }
]

const cursorStyle = computed(() => {
  if (currentTool.value === 'text') return 'text'
  if (currentTool.value === 'eraser') return 'cell'
  return 'crosshair'
})

const rootStyle = computed(() => {
  return {
    width: dipWidth.value + 'px',
    height: dipHeight.value + 'px',
    backgroundColor: isTransparent.value ? 'transparent' : '#000'
  }
})

let unsubInit: (() => void) | null = null

onMounted(() => {
  // 通知主进程页面就绪
  window.sidekick.overlay.ready()

  // 监听初始化数据
  unsubInit = window.sidekick.overlay.onInit((init: any) => {
    if (init.mode !== 'annotate') return

    scaleFactor.value = init.scaleFactor || 1
    dipWidth.value = init.dipWidth || window.innerWidth
    dipHeight.value = init.dipHeight || window.innerHeight
    // canvas 物理分辨率 = DIP 尺寸 × 系统缩放因子
    canvasWidth.value = Math.round(dipWidth.value * scaleFactor.value)
    canvasHeight.value = Math.round(dipHeight.value * scaleFactor.value)

    // 透明模式: 无背景图, 立即就绪, 秒开秒画
    if (init.transparent) {
      isTransparent.value = true
      ready.value = true
      nextTick(() => initCanvas())
      return
    }

    // 兼容旧版: 若传了背景图则等加载
    if (init.screenshotDataUrl) {
      // 旧模式: 不实现, 直接按透明模式处理
      isTransparent.value = true
      ready.value = true
      nextTick(() => initCanvas())
    }
  })

  // ESC 取消
  document.addEventListener('keydown', onKey)
})

onUnmounted(() => {
  if (unsubInit) unsubInit()
  document.removeEventListener('keydown', onKey)
})

function initCanvas() {
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  ctx.lineCap = 'round'
  ctx.lineJoin = 'round'
}

function toggleBg() {
  bgOpacity.value = bgOpacity.value > 0 ? 0 : 40
}

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    if (textEditing.value) {
      cancelText()
    } else {
      cancel()
    }
  }
  // Ctrl+Z 撤销
  if (e.ctrlKey && e.key === 'z') {
    e.preventDefault()
    undo()
  }
}

function getCanvasCoords(e: MouseEvent): { x: number; y: number } {
  const canvas = canvasRef.value!
  const rect = canvas.getBoundingClientRect()
  const scaleX = canvas.width / rect.width
  const scaleY = canvas.height / rect.height
  return {
    x: (e.clientX - rect.left) * scaleX,
    y: (e.clientY - rect.top) * scaleY
  }
}

function saveUndoState() {
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height)
  undoStack.push(imageData)
  if (undoStack.length > MAX_UNDO) {
    undoStack.shift()
  }
}

function onMouseDown(e: MouseEvent) {
  if (!ready.value) return
  const { x, y } = getCanvasCoords(e)

  if (currentTool.value === 'text') {
    // 开始文字编辑
    textEditing.value = true
    textInput.value = ''
    textX.value = e.clientX
    textY.value = e.clientY
    textInputStyle.value = {
      left: e.clientX + 'px',
      top: (e.clientY - 14) + 'px',
      color: currentColor.value,
      fontSize: (currentSize.value * 6) + 'px'
    }
    nextTick(() => textInputRef.value?.focus())
    return
  }

  // 保存撤销状态
  saveUndoState()

  isDrawing.value = true
  startX.value = x
  startY.value = y

  const ctx = canvasRef.value!.getContext('2d')!

  if (currentTool.value === 'pen' || currentTool.value === 'highlighter' || currentTool.value === 'eraser') {
    ctx.beginPath()
    ctx.moveTo(x, y)

    if (currentTool.value === 'highlighter') {
      ctx.globalAlpha = 0.35
      ctx.lineWidth = currentSize.value * 5
    } else if (currentTool.value === 'eraser') {
      ctx.globalCompositeOperation = 'destination-out'
      ctx.lineWidth = currentSize.value * 8
    } else {
      ctx.globalAlpha = 1
      ctx.lineWidth = currentSize.value
    }
    ctx.strokeStyle = currentColor.value
  } else if (currentTool.value === 'arrow' || currentTool.value === 'rect') {
    // 保存当前画布状态用于预览
    savedImageData = ctx.getImageData(0, 0, canvasRef.value!.width, canvasRef.value!.height)
  }
}

function onMouseMove(e: MouseEvent) {
  if (!isDrawing.value || !ready.value) return
  const { x, y } = getCanvasCoords(e)
  const ctx = canvasRef.value!.getContext('2d')!

  if (currentTool.value === 'pen' || currentTool.value === 'highlighter' || currentTool.value === 'eraser') {
    ctx.lineTo(x, y)
    ctx.stroke()
  } else if (currentTool.value === 'arrow') {
    // 恢复保存的状态, 绘制预览
    if (savedImageData) ctx.putImageData(savedImageData, 0, 0)
    ctx.globalAlpha = 1
    ctx.globalCompositeOperation = 'source-over'
    ctx.lineWidth = currentSize.value
    ctx.strokeStyle = currentColor.value
    drawArrow(ctx, startX.value, startY.value, x, y)
  } else if (currentTool.value === 'rect') {
    if (savedImageData) ctx.putImageData(savedImageData, 0, 0)
    ctx.globalAlpha = 1
    ctx.globalCompositeOperation = 'source-over'
    ctx.lineWidth = currentSize.value
    ctx.strokeStyle = currentColor.value
    ctx.strokeRect(
      Math.min(startX.value, x),
      Math.min(startY.value, y),
      Math.abs(x - startX.value),
      Math.abs(y - startY.value)
    )
  }
}

function onMouseUp(e: MouseEvent) {
  if (!isDrawing.value) return
  isDrawing.value = false

  const ctx = canvasRef.value!.getContext('2d')!

  // 重置上下文状态
  ctx.globalAlpha = 1
  ctx.globalCompositeOperation = 'source-over'

  if (currentTool.value === 'arrow' || currentTool.value === 'rect') {
    savedImageData = null
  }
}

function drawArrow(ctx: CanvasRenderingContext2D, x1: number, y1: number, x2: number, y2: number) {
  const headLen = 20
  const angle = Math.atan2(y2 - y1, x2 - x1)

  ctx.beginPath()
  ctx.moveTo(x1, y1)
  ctx.lineTo(x2, y2)
  ctx.stroke()

  // 箭头头部
  ctx.beginPath()
  ctx.moveTo(x2, y2)
  ctx.lineTo(x2 - headLen * Math.cos(angle - Math.PI / 6), y2 - headLen * Math.sin(angle - Math.PI / 6))
  ctx.moveTo(x2, y2)
  ctx.lineTo(x2 - headLen * Math.cos(angle + Math.PI / 6), y2 - headLen * Math.sin(angle + Math.PI / 6))
  ctx.stroke()
}

function commitText() {
  if (!textEditing.value || !textInput.value.trim()) {
    textEditing.value = false
    return
  }

  const canvas = canvasRef.value
  if (!canvas) {
    textEditing.value = false
    return
  }

  const ctx = canvas.getContext('2d')!
  saveUndoState()

  const fontSize = currentSize.value * 6
  ctx.globalAlpha = 1
  ctx.globalCompositeOperation = 'source-over'
  ctx.fillStyle = currentColor.value
  ctx.font = `bold ${fontSize}px "Microsoft YaHei UI", "Segoe UI", sans-serif`
  ctx.textBaseline = 'top'

  // 将 DIP 坐标转换为 canvas 坐标
  const rect = canvas.getBoundingClientRect()
  const scaleX = canvas.width / rect.width
  const scaleY = canvas.height / rect.height
  const cx = textX.value * scaleX
  const cy = (textY.value - 14) * scaleY

  ctx.fillText(textInput.value, cx, cy)

  textEditing.value = false
  textInput.value = ''
}

function cancelText() {
  textEditing.value = false
  textInput.value = ''
}

function undo() {
  if (undoStack.length === 0) return
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')!
  const imageData = undoStack.pop()!
  ctx.putImageData(imageData, 0, 0)
}

function save() {
  const canvas = canvasRef.value
  if (!canvas) return

  // 透明模式: 直接导出画布 PNG (只含批注内容)
  const dataUrl = canvas.toDataURL('image/png')
  window.sidekick.overlay.saveAnnotate(dataUrl)
}

function cancel() {
  window.sidekick.overlay.cancel()
}
</script>

<style scoped>
.annotate-root {
  position: fixed;
  top: 0;
  left: 0;
  overflow: hidden;
  background: transparent;
}

.annotate-root.with-bg {
  background: #f0f0f0;
}

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

.indicator-text {
  font-weight: 500;
}

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
}

.toolbar {
  position: fixed;
  bottom: 24px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  background: rgba(30, 30, 30, 0.92);
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.4);
  z-index: 10;
  backdrop-filter: blur(8px);
}

.tool-group, .color-group, .size-group {
  display: flex;
  gap: 4px;
}

.divider {
  width: 1px;
  height: 20px;
  background: rgba(255, 255, 255, 0.15);
  margin: 0 4px;
}

.tool-btn {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  background: transparent;
  color: #ccc;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  border: 1px solid transparent;
  transition: all 120ms ease;
}

.tool-btn:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}

.tool-btn.active {
  background: var(--brand, #2B6EE0);
  color: #fff;
  border-color: rgba(255, 255, 255, 0.3);
}

.color-btn {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  border: 2px solid rgba(255, 255, 255, 0.2);
  transition: all 120ms ease;
}

.color-btn:hover {
  transform: scale(1.15);
}

.color-btn.active {
  border-color: #fff;
  box-shadow: 0 0 0 2px rgba(43, 110, 224, 0.5);
}

.size-btn {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  background: transparent;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid transparent;
  transition: all 120ms ease;
}

.size-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.size-btn.active {
  background: rgba(43, 110, 224, 0.3);
  border-color: rgba(255, 255, 255, 0.2);
}

.size-dot {
  background: #ccc;
  border-radius: 50%;
}

/* 背景遮罩控制 */
.bg-control {
  display: flex;
  align-items: center;
  gap: 8px;
}

.bg-toggle {
  padding: 4px 10px;
  border-radius: 6px;
  font-size: 12px;
  color: #ccc;
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.2);
  transition: all 120ms ease;
  white-space: nowrap;
}

.bg-toggle:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}

.bg-toggle.active {
  background: rgba(43, 110, 224, 0.4);
  border-color: rgba(43, 110, 224, 0.6);
  color: #fff;
}

.bg-slider {
  width: 60px;
  height: 4px;
  accent-color: #2B6EE0;
  cursor: pointer;
}

.action-btn {
  padding: 6px 14px;
  border-radius: 8px;
  font-size: 13px;
  color: #ccc;
  background: transparent;
  border: 1px solid transparent;
  transition: all 120ms ease;
}

.action-btn:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}

.action-btn.undo:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.action-btn.save {
  background: #27ae60;
  color: #fff;
}

.action-btn.save:hover {
  background: #229954;
}

.action-btn.cancel {
  background: rgba(231, 76, 60, 0.8);
  color: #fff;
}

.action-btn.cancel:hover {
  background: #c0392b;
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

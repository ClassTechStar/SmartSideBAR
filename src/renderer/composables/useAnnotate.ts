// composables/useAnnotate.ts - 批注绘图逻辑 (P2-10 拆分自 AnnotateApp.vue)
// 封装画布状态、绘图工具、指针输入、撤销、导出等全部逻辑, 可独立单测。

import { ref, computed, nextTick } from 'vue'

export type AnnotateTool = 'pen' | 'highlighter' | 'arrow' | 'rect' | 'text' | 'eraser'

export const ANNOTATE_TOOLS: Array<{ id: AnnotateTool; label: string; icon: string }> = [
  { id: 'pen', label: '画笔', icon: '✏️' },
  { id: 'highlighter', label: '高亮笔', icon: '🖊️' },
  { id: 'arrow', label: '箭头', icon: '➤' },
  { id: 'rect', label: '矩形', icon: '▭' },
  { id: 'text', label: '文字', icon: 'T' },
  { id: 'eraser', label: '橡皮擦', icon: '🧹' }
]

export const ANNOTATE_COLORS = ['#e74c3c', '#2B6EE0', '#27ae60', '#f39c12', '#ffffff', '#000000']
export const ANNOTATE_SIZES = [
  { value: 2, label: '细' },
  { value: 4, label: '中' },
  { value: 8, label: '粗' }
]

const JITTER_PX = 4
const MAX_UNDO = 30

export function useAnnotate() {
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

  // P1-8: 屏幕背景截图, 供「含截图」导出模式使用
  const hasBackground = ref(false)
  let bgImage: HTMLImageElement | null = null

  const currentTool = ref<AnnotateTool>('pen')
  const currentColor = ref('#e74c3c')
  const currentSize = ref(3)

  const isDrawing = ref(false)
  const startX = ref(0)
  const startY = ref(0)
  let savedImageData: ImageData | null = null

  // D3 修复: 撤销栈保持普通数组 (ImageData 为 DOM 对象, ref 深度响应式会做 Proxy 包装,
  // putImageData 收到 Proxy 对象行为不可靠); 按钮禁用态改由 canUndo ref 驱动
  const undoStack: ImageData[] = []
  const canUndo = ref(false)

  // 文字编辑
  const textEditing = ref(false)
  const textInput = ref('')
  const textX = ref(0)
  const textY = ref(0)
  const textInputStyle = ref<Record<string, string>>({})

  const cursorStyle = computed(() => {
    if (currentTool.value === 'text') return 'text'
    if (currentTool.value === 'eraser') return 'cell'
    return 'crosshair'
  })

  function initCanvas() {
    const canvas = canvasRef.value
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    ctx.lineCap = 'round'
    ctx.lineJoin = 'round'
  }

  /** 由外部 (AnnotateApp onInit) 设置画布尺寸并初始化 */
  function setupCanvas(dipW: number, dipH: number, sf: number, transparent: boolean) {
    scaleFactor.value = sf
    dipWidth.value = dipW
    dipHeight.value = dipH
    canvasWidth.value = Math.round(dipW * sf)
    canvasHeight.value = Math.round(dipH * sf)
    isTransparent.value = transparent
    ready.value = true
    nextTick(() => initCanvas())
  }

  function setBackground(dataUrl: string) {
    const img = new Image()
    img.onload = () => {
      bgImage = img
      hasBackground.value = true
    }
    img.src = dataUrl
  }

  function toggleBg() {
    bgOpacity.value = bgOpacity.value > 0 ? 0 : 40
  }

  function getCanvasCoords(e: { clientX: number; clientY: number }): { x: number; y: number } {
    const canvas = canvasRef.value!
    const rect = canvas.getBoundingClientRect()
    const scaleX = canvas.width / rect.width
    const scaleY = canvas.height / rect.height
    return { x: (e.clientX - rect.left) * scaleX, y: (e.clientY - rect.top) * scaleY }
  }

  function saveUndoState() {
    const canvas = canvasRef.value
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height)
    undoStack.push(imageData)
    if (undoStack.length > MAX_UNDO) undoStack.shift()
    canUndo.value = undoStack.length > 0
  }

  // ---- P0-5: 指针输入统一处理 (鼠标 / 触摸 / 触控笔) ----
  let activePointerId: number | null = null
  let lastDrawX = 0
  let lastDrawY = 0

  function onPointerDown(e: PointerEvent) {
    if (!ready.value) return
    if (activePointerId !== null) return
    activePointerId = e.pointerId
    if (e.pointerType !== 'mouse') e.preventDefault()
    beginStroke(e)
  }

  function onPointerMove(e: PointerEvent) {
    if (e.pointerId !== activePointerId) return
    moveStroke(e)
  }

  function onPointerUp(e: PointerEvent) {
    if (e.pointerId !== activePointerId) return
    activePointerId = null
    endStroke()
  }

  function beginStroke(e: { clientX: number; clientY: number }) {
    if (!ready.value) return
    const { x, y } = getCanvasCoords(e)

    if (currentTool.value === 'text') {
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

    saveUndoState()
    isDrawing.value = true
    startX.value = x
    startY.value = y
    lastDrawX = x
    lastDrawY = y

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
      savedImageData = ctx.getImageData(0, 0, canvasRef.value!.width, canvasRef.value!.height)
    }
  }

  function moveStroke(e: { clientX: number; clientY: number }) {
    if (!isDrawing.value || !ready.value) return
    const { x, y } = getCanvasCoords(e)
    const ctx = canvasRef.value!.getContext('2d')!

    if (currentTool.value === 'pen' || currentTool.value === 'highlighter' || currentTool.value === 'eraser') {
      if (Math.abs(x - lastDrawX) < JITTER_PX && Math.abs(y - lastDrawY) < JITTER_PX) return
      lastDrawX = x
      lastDrawY = y
      ctx.lineTo(x, y)
      ctx.stroke()
    } else if (currentTool.value === 'arrow') {
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

  function endStroke() {
    if (!isDrawing.value) return
    isDrawing.value = false
    const ctx = canvasRef.value!.getContext('2d')!
    ctx.globalAlpha = 1
    ctx.globalCompositeOperation = 'source-over'
    if (currentTool.value === 'arrow' || currentTool.value === 'rect') savedImageData = null
  }

  function drawArrow(ctx: CanvasRenderingContext2D, x1: number, y1: number, x2: number, y2: number) {
    const headLen = 20
    const angle = Math.atan2(y2 - y1, x2 - x1)
    ctx.beginPath()
    ctx.moveTo(x1, y1)
    ctx.lineTo(x2, y2)
    ctx.stroke()
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
    if (!canvas) { textEditing.value = false; return }

    const ctx = canvas.getContext('2d')!
    saveUndoState()
    const fontSize = currentSize.value * 6
    ctx.globalAlpha = 1
    ctx.globalCompositeOperation = 'source-over'
    ctx.fillStyle = currentColor.value
    ctx.font = `bold ${fontSize}px "Microsoft YaHei UI", "Segoe UI", sans-serif`
    ctx.textBaseline = 'top'

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
    canUndo.value = undoStack.length > 0
  }

  // P1-8: 仅笔迹导出 (透明背景, 只含批注内容)
  function saveAnnotationsOnly() {
    const canvas = canvasRef.value
    if (!canvas) return
    window.sidekick.overlay.saveAnnotate(canvas.toDataURL('image/png'))
  }

  // P1-8: 含屏幕背景导出 (截图 + 笔迹合成)
  function saveWithBackground() {
    const canvas = canvasRef.value
    if (!canvas || !bgImage) return
    const tempCanvas = document.createElement('canvas')
    tempCanvas.width = canvas.width
    tempCanvas.height = canvas.height
    const ctx = tempCanvas.getContext('2d')
    if (!ctx) return
    ctx.drawImage(bgImage, 0, 0, tempCanvas.width, tempCanvas.height)
    ctx.drawImage(canvas, 0, 0)
    window.sidekick.overlay.saveAnnotate(tempCanvas.toDataURL('image/png'))
  }

  return {
    canvasRef, textInputRef,
    ready, isTransparent, bgOpacity, dipWidth, dipHeight, canvasWidth, canvasHeight, scaleFactor,
    hasBackground, currentTool, currentColor, currentSize, isDrawing, canUndo,
    textEditing, textInput, textInputStyle,
    cursorStyle,
    setupCanvas, setBackground, toggleBg,
    onPointerDown, onPointerMove, onPointerUp,
    commitText, cancelText, undo, saveAnnotationsOnly, saveWithBackground
  }
}

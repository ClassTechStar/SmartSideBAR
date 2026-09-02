<template>
  <div
    class="fb-root"
    :class="{ expanded: isExpanded, idle, dragging }"
    @mouseenter="onEnter"
    @mouseleave="onLeave"
    @mousemove="onMove"
  >
    <LiquidGlassDefs :lens-map="lensMap" />
    <button
      v-for="(it, i) in items"
      :key="it.id"
      class="fb-item lg-glass lg-item lg-liquid fb-interactive"
      :class="{ show: isExpanded }"
      :style="itemStyle(i)"
      :title="it.title"
      @click="onItemClick(it.id)"
    >
      <img :src="it.icon" class="fb-item-icon" draggable="false" />
    </button>
    <div
      class="fb-orb fb-interactive"
      :class="{ dragging }"
      :style="orbStyle"
      @pointerdown="onOrbDown"
      @pointerup="onOrbUp"
      @pointercancel="onOrbUp"
      @dblclick="onOrbDblClick"
      @mousemove="onOrbMove"
      @mouseleave="onOrbLeave"
    >
      <div class="fb-orb-ring"></div>
      <div class="fb-orb-core"></div>
      <span v-if="recordActive" class="fb-rec-ring">{{ recText }}</span>
      <span v-if="imeBadge" class="fb-ime-badge">{{ imeBadge }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
// 悬浮球 (FloatBall) —— 从 v1.1 编译产物重建。
// 透明 + focusable:false 窗口, 拖拽/吸附/扇形菜单由主进程驱动 (floatball:layout 下发)。
import { ref, computed, onMounted, onUnmounted } from 'vue'
import LiquidGlassDefs from '../glass/LiquidGlassDefs.vue'
import { makeLensMap } from '../composables/useGlass'
import { fanItemOffset, FAN_START_DEG, FAN_SWEEP_DEG } from '@shared/floatball-layout'

const ICONS: Record<string, string> = {
  capture: 'capture',
  annotate: 'annotate',
  longshot: 'longshot',
  record: 'record',
  ime: 'ime',
  taskmgr: 'taskmgr',
  sidebar: 'sidebar',
  settings: 'settings'
}
const TITLES: Record<string, string> = {
  capture: '区域截图',
  annotate: '批注',
  longshot: '长截图',
  record: '录屏',
  ime: '切换输入法',
  taskmgr: '任务管理器',
  sidebar: '侧边栏',
  settings: '设置'
}

interface Layout {
  expanded: boolean
  ballSize: number
  ballOffset: { x: number; y: number }
  dir: { x: number; y: number }
  radius: number
  itemSize: number
}

const layout = ref<Layout | null>(null)
const actionList = ref<string[]>([])
const idleDelayMs = ref(4000)
const idleOpacity = ref(0.55)
const doubleClick = ref('toggleSidebar')
const isExpanded = ref(false)
const idle = ref(false)
const dragging = ref(false)
const recordActive = ref(false)
const recElapsed = ref(0)
const imeBadge = ref('')
const lensMap = ref('')
const mx = ref(0)
const my = ref(0)
let idleTimer: ReturnType<typeof setTimeout> | null = null
let recTimer: ReturnType<typeof setInterval> | null = null
let lastClickThrough = true

const items = computed(() =>
  actionList.value.map((id) => ({ id, icon: iconFor(id), title: TITLES[id] || id }))
)

const orbStyle = computed(() => {
  const l = layout.value
  if (!l) return {}
  const op = idle.value ? idleOpacity.value : 1
  return {
    left: `${l.ballOffset.x}px`,
    top: `${l.ballOffset.y}px`,
    width: `${l.ballSize}px`,
    height: `${l.ballSize}px`,
    opacity: String(op),
    '--lg-mx': String(mx.value),
    '--lg-my': String(my.value)
  }
})

function itemStyle(i: number): Record<string, string> {
  const l = layout.value
  if (!l) return {}
  const itemSize = l.itemSize
  const cx = l.ballOffset.x + l.ballSize / 2
  const cy = l.ballOffset.y + l.ballSize / 2
  if (!l.expanded) {
    return {
      left: `${cx - itemSize / 2}px`,
      top: `${cy - itemSize / 2}px`,
      width: `${itemSize}px`,
      height: `${itemSize}px`,
      opacity: '0',
      transform: 'scale(0.2)',
      pointerEvents: 'none'
    }
  }
  const off = fanItemOffset(i, actionList.value.length, l.radius, l.dir, FAN_START_DEG, FAN_SWEEP_DEG)
  return {
    left: `${cx + off.x - itemSize / 2}px`,
    top: `${cy + off.y - itemSize / 2}px`,
    width: `${itemSize}px`,
    height: `${itemSize}px`,
    opacity: '1',
    transform: 'scale(1)',
    pointerEvents: 'auto'
  }
}

const recText = computed(() => {
  const s = recElapsed.value
  const m = Math.floor(s / 60)
  const sec = s % 60
  return `${m}:${sec.toString().padStart(2, '0')}`
})

function iconFor(id: string): string {
  return new URL(`../assets/icons/${ICONS[id] || id}.svg`, import.meta.url).href
}

async function loadConfig(): Promise<void> {
  try {
    const cfg = await window.sidekick.config.get()
    actionList.value = cfg.floatBall.actions || []
    idleDelayMs.value = cfg.floatBall.idleDelayMs
    idleOpacity.value = cfg.floatBall.idleOpacity
    doubleClick.value = cfg.floatBall.doubleClick
  } catch (e) {
    console.warn('[FloatBall] load config failed:', e)
  }
}

function resetIdle(): void {
  idle.value = false
  if (idleTimer) clearTimeout(idleTimer)
  if (idleDelayMs.value > 0) {
    idleTimer = setTimeout(() => {
      idle.value = true
    }, idleDelayMs.value)
  }
}

function setClickThrough(on: boolean): void {
  if (on === lastClickThrough) return
  lastClickThrough = on
  try {
    window.sidekick.floatball.setClickThrough(on)
  } catch { /* ignore */ }
}

function onEnter(): void {
  if (dragging.value) return
  resetIdle()
  if (!isExpanded.value) window.sidekick.floatball.expand()
}
function onLeave(): void {
  if (dragging.value) return
  if (isExpanded.value && items.value.length === 0) {
    window.sidekick.floatball.collapse()
  }
}
function onMove(e: MouseEvent): void {
  resetIdle()
  if (isExpanded.value) {
    const t = e.target as HTMLElement
    const interactive = !!t.closest('.fb-interactive')
    setClickThrough(!interactive)
  }
}
function onOrbDown(e: PointerEvent): void {
  dragging.value = true
  const grab = { x: (e as any).offsetX, y: (e as any).offsetY }
  window.sidekick.floatball.expand()
  window.sidekick.floatball.dragStart(grab)
}
function onOrbUp(): void {
  if (!dragging.value) return
  dragging.value = false
  window.sidekick.floatball.dragEnd()
}
function onOrbMove(e: MouseEvent): void {
  const r = (e.currentTarget as HTMLElement).getBoundingClientRect()
  const nx = ((e.clientX - r.left - r.width / 2) / r.width) * 100
  const ny = ((e.clientY - r.top - r.height / 2) / r.height) * 100
  mx.value = Math.max(-100, Math.min(100, nx))
  my.value = Math.max(-100, Math.min(100, ny))
}
function onOrbLeave(): void {
  if (dragging.value) return
  mx.value = 0
  my.value = 0
}
function onOrbDblClick(): void {
  if (doubleClick.value === 'toggleSidebar') window.sidekick.floatball.action('sidebar')
  else if (doubleClick.value === 'capture') window.sidekick.floatball.action('capture')
}
function onItemClick(id: string): void {
  window.sidekick.floatball.action(id)
  setTimeout(() => {
    window.sidekick.floatball.collapse()
  }, 150)
}

let unLayout: (() => void) | null = null
let unIme: (() => void) | null = null

onMounted(async () => {
  await loadConfig()
  lensMap.value = makeLensMap(128)
  resetIdle()
  unLayout = window.sidekick.floatball.onLayout((l: Layout) => {
    layout.value = l
    isExpanded.value = l.expanded
    if (l.expanded) {
      lastClickThrough = false
      setClickThrough(true)
    } else {
      setClickThrough(false)
    }
  })
  try {
    const st = await window.sidekick.ime.getState()
    imeBadge.value = st.mode === 'cn' ? '中' : 'EN'
    unIme = window.sidekick.ime.onChanged((s: any) => {
      imeBadge.value = s.mode === 'cn' ? '中' : 'EN'
    })
  } catch { /* degraded */ }
  try {
    window.sidekick.recorder.onStatusChanged((s: any) => {
      if (s?.recording) {
        recordActive.value = true
        recElapsed.value = s.elapsed || 0
        if (!recTimer) recTimer = setInterval(() => { recElapsed.value++ }, 1000)
      } else {
        recordActive.value = false
        if (recTimer) {
          clearInterval(recTimer)
          recTimer = null
        }
      }
    })
  } catch { /* degraded */ }
})

onUnmounted(() => {
  if (idleTimer) clearTimeout(idleTimer)
  if (recTimer) clearInterval(recTimer)
  unLayout?.()
  unIme?.()
})
</script>

<!-- 悬浮球样式已迁移到全局 styles/floatball.css (v1.1 液态玻璃, 去 scoped hash) -->

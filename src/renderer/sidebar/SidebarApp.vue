<template>
  <div
    class="sidebar"
    :class="{ docked: isDocked }"
    :style="{ '--lg-mx': String(sx), '--lg-my': String(sy) }"
    @mouseenter="onMouseEnter"
    @mouseleave="onMouseLeave"
    @mousemove="onSidebarMove"
  >
    <LiquidGlassDefs :lens-map="lensMap" />
    <!-- 图标轨 (垂直/水平自适应) -->
    <div class="rail lg-glass lg-thin lg-rim" :class="{ docked: isDocked }" @dblclick="onRailDblClick">
      <div class="rail-inner" :class="{ docked: isDocked }">
        <div
          class="rail-item"
          v-for="item in railItems"
          :key="item.id"
          :class="{ active: activePanel === item.panel && !isDocked }"
          @click="onRailClick(item)"
          :title="item.title"
        >
          <img :src="item.icon" :alt="item.title" class="rail-icon" />
          <span v-if="item.badge && !isDocked" class="rail-badge" :class="item.badgeClass">{{ item.badge }}</span>
        </div>
      </div>

      <!-- 收起/展开 切换按钮 -->
      <div class="dock-toggle" :class="{ docked: isDocked }" @click="toggleDock" :title="isDocked ? '展开侧边栏' : '收起侧边栏'">
        <!-- 箭头用内联 SVG: 与 rail 图标 (assets/icons/*.svg) 完全同规格
             —— 24 视框 / stroke-width 2 / 22x22 渲染尺寸。
             原实现用字符 ↑ ↓, 笔画粗细由系统字体决定且随字体浮动,
             无法与图标线条对齐(见 E12 / P2-11)。 -->
        <svg
          v-if="isDocked"
          class="dock-arrow"
          viewBox="0 0 24 24" fill="none" stroke="currentColor"
          stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
          aria-hidden="true"
        >
          <line x1="12" y1="19" x2="12" y2="5" />
          <polyline points="5 12 12 5 19 12" />
        </svg>
        <svg
          v-else
          class="dock-arrow"
          viewBox="0 0 24 24" fill="none" stroke="currentColor"
          stroke-width="2" stroke-linecap="round" stroke-linejoin="round"
          aria-hidden="true"
        >
          <line x1="12" y1="5" x2="12" y2="19" />
          <polyline points="19 12 12 19 5 12" />
        </svg>
      </div>
    </div>

    <!-- 右侧展开面板 (仅 expanded 且未 docked 时渲染) -->
    <div class="panel lg-glass lg-thin lg-rim" v-show="isExpanded && !isDocked" :class="{ expanded: isExpanded }">
      <div class="panel-header">
        <span class="panel-title">{{ panelTitle }}</span>
        <button class="panel-close" @click="collapse">×</button>
      </div>
      <div class="panel-body">
        <component :is="currentPanel" v-if="currentPanel" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, shallowRef, watch } from 'vue'
import ImePanel from '../components/ImePanel.vue'
import CapturePanel from '../components/CapturePanel.vue'
import UsbPanel from '../components/UsbPanel.vue'
import PrinterPanel from '../components/PrinterPanel.vue'
import LinksPanel from '../components/LinksPanel.vue'
import ReminderPanel from '../components/ReminderPanel.vue'
import TaskMgrPanel from '../components/TaskMgrPanel.vue'
import SettingsPanel from '../components/SettingsPanel.vue'
import LiquidGlassDefs from '../glass/LiquidGlassDefs.vue'
import { makeLensMap } from '../composables/useGlass'

const RAIL_WIDTH = 72
const PANEL_WIDTH = 380
const EXPANDED_WIDTH = RAIL_WIDTH + PANEL_WIDTH

const isExpanded = ref(false)
const isDocked = ref(false)
const activePanel = ref<string>('')
const imeMode = ref<'cn' | 'en'>('cn')
// 液态玻璃: 光标归一化偏移 (驱动 --lg-mx/--lg-my 高光跟随) + 折射位移贴图
const sx = ref(0)
const sy = ref(0)
const lensMap = ref('')
// P2-1: 策略禁用的模块集合 (来自 config.policy.disabledModules)
const disabledModules = ref<string[]>([])
let expandTimer: ReturnType<typeof setTimeout> | null = null
let collapseTimer: ReturnType<typeof setTimeout> | null = null

// ---- computed -------------------------------------------------
const panelTitle = computed(() => {
  const titles: Record<string, string> = {
    ime: '输入法',
    capture: '截图与批注',
    usb: 'USB 设备',
    printer: '打印机',
    links: '快捷链接',
    reminder: '定时提醒',
    taskmgr: '任务管理',
    settings: '设置'
  }
  return titles[activePanel.value] || ''
})

const currentPanel = shallowRef<any>(null)

const panelMap: Record<string, any> = {
  ime: ImePanel,
  capture: CapturePanel,
  usb: UsbPanel,
  printer: PrinterPanel,
  links: LinksPanel,
  reminder: ReminderPanel,
  taskmgr: TaskMgrPanel,
  settings: SettingsPanel
}

const railItems = computed(() => {
  // P2-1: 白名单策略落地 —— 每个 rail item 标注所属模块, 被 policy.disabledModules
  // 点名的模块从 rail 隐藏 (与主进程服务启动过滤一致)。
  const disabled = (m: string) => disabledModules.value.includes(m)
  return [
    { id: 'ime', module: 'ime', panel: 'ime', title: '输入法切换',
      icon: getIconPath('ime'),
      action: () => toggleIme(),
      badge: imeMode.value === 'cn' ? '中' : 'EN',
      badgeClass: imeMode.value === 'cn' ? 'badge-cn' : 'badge-en' },
    { id: 'capture', module: 'capture', panel: 'capture', title: '区域截图', icon: getIconPath('capture'), action: () => expandPanel('capture') },
    { id: 'annotate', module: 'capture', panel: 'capture', title: '批注', icon: getIconPath('annotate'), action: () => expandPanel('capture') },
    { id: 'longshot', module: 'capture', panel: 'capture', title: '长截图', icon: getIconPath('longshot'), action: () => expandPanel('capture') },
    { id: 'record', module: 'capture', panel: 'capture', title: '录屏', icon: getIconPath('record'), action: () => expandPanel('capture') },
    { id: 'usb', module: 'usb', panel: 'usb', title: 'USB 监控', icon: getIconPath('usb'), action: () => expandPanel('usb') },
    { id: 'printer', module: 'printer', panel: 'printer', title: '打印机', icon: getIconPath('printer'), action: () => expandPanel('printer') },
    { id: 'taskmgr', module: 'taskmgr', panel: 'taskmgr', title: '任务管理器', icon: getIconPath('taskmgr'), action: () => openTaskMgr() },
    { id: 'link', module: 'links', panel: 'links', title: '快捷链接', icon: getIconPath('link'), action: () => expandPanel('links') },
    { id: 'bell', module: 'reminder', panel: 'reminder', title: '定时提醒', icon: getIconPath('bell'), action: () => expandPanel('reminder') },
    { id: 'settings', module: 'settings', panel: 'settings', title: '设置', icon: getIconPath('settings'), action: () => expandPanel('settings') }
  ].filter(item => !disabled(item.module))
})

// ---- watch: 窗口尺寸同步 --------------------------------------
watch(isExpanded, async (expanded) => {
  if (isDocked.value) return // docked 时不处理展开/收缩
  try {
    if (expanded) {
      await window.sidekick.window.resize(EXPANDED_WIDTH, 0)
    } else {
      await window.sidekick.window.resize(RAIL_WIDTH, 0)
    }
  } catch (e) {
    console.warn('[Sidebar] Resize IPC failed:', e)
  }
})

// ---- helpers --------------------------------------------------
function getIconPath(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

function onMouseEnter() {
  if (isDocked.value) return // docked 时禁止 hover 展开
  if (expandTimer) clearTimeout(expandTimer)
  if (collapseTimer) clearTimeout(collapseTimer)
}

function onMouseLeave() {
  if (isDocked.value) return
  sx.value = 0
  sy.value = 0
  if (isExpanded.value) {
    if ((window as any).__keepSidebarOpen) return
    collapseTimer = setTimeout(() => {
      if ((window as any).__keepSidebarOpen) return
      collapse()
    }, 5000)
  }
}

// 液态玻璃: 光标相对面板中心的归一化偏移 (-100~100), 写入 --lg-mx/--lg-my
function onSidebarMove(e: MouseEvent) {
  const r = (e.currentTarget as HTMLElement).getBoundingClientRect()
  sx.value = Math.max(-100, Math.min(100, ((e.clientX - r.left - r.width / 2) / r.width) * 100))
  sy.value = Math.max(-100, Math.min(100, ((e.clientY - r.top - r.height / 2) / r.height) * 100))
}

function expandPanel(name: string) {
  if (isDocked.value) {
    // docked 状态下先展开侧边栏，再执行动作
    undock()
    setTimeout(() => {
      activePanel.value = name
      currentPanel.value = panelMap[name]
      isExpanded.value = true
    }, 350)
    return
  }
  activePanel.value = name
  currentPanel.value = panelMap[name]
  isExpanded.value = true
}

function collapse() {
  isExpanded.value = false
  activePanel.value = ''
  currentPanel.value = null
}

async function toggleDock() {
  if (isDocked.value) {
    await undock()
  } else {
    await dock()
  }
}

async function dock() {
  isDocked.value = true
  isExpanded.value = false
  activePanel.value = ''
  currentPanel.value = null
  try {
    await window.sidekick.window.dock()
  } catch (e) {
    console.warn('[Sidebar] dock failed:', e)
  }
}

async function undock() {
  isDocked.value = false
  try {
    await window.sidekick.window.undock()
  } catch (e) {
    console.warn('[Sidebar] undock failed:', e)
  }
}

function onRailClick(item: any) {
  if (isDocked.value) {
    undock()
    setTimeout(() => item.action(), 350)
  } else {
    item.action()
  }
}

function onRailDblClick(event: MouseEvent) {
  // 只要双击路径中不包含 .rail-item 和 .dock-toggle，就视为空白处双击
  const path = event.composedPath()
  const hitItem = path.some(el => el instanceof Element && el.classList.contains('rail-item'))
  const hitToggle = path.some(el => el instanceof Element && el.classList.contains('dock-toggle'))
  if (hitItem || hitToggle) return
  if (isDocked.value) {
    undock()
  } else {
    dock()
  }
}

async function toggleIme() {
  try {
    const state = await window.sidekick.ime.toggle()
    imeMode.value = state.mode
  } catch (e) {
    console.error('IME toggle failed:', e)
  }
}

async function openTaskMgr() {
  try {
    await window.sidekick.app.openTaskMgr()
  } catch (e) {
    console.error('Open taskmgr failed:', e)
  }
}

// ---- lifecycle ------------------------------------------------
let imeUnsub: (() => void) | null = null

onMounted(async () => {
  lensMap.value = makeLensMap(128)
  // P2-1: 读取策略禁用模块, railItems 据此隐藏对应图标
  try {
    const cfg = await window.sidekick.config.get()
    disabledModules.value = cfg?.policy?.disabledModules || []
  } catch { /* 忽略, 默认不禁用 */ }

  try {
    const state = await window.sidekick.ime.getState()
    imeMode.value = state.mode
  } catch { /* degraded */ }

  imeUnsub = window.sidekick.ime.onChanged((state: any) => {
    imeMode.value = state.mode
  })
})

onUnmounted(() => {
  if (imeUnsub) imeUnsub()
})
</script>

<!-- 侧边栏样式已迁移到全局 styles/sidebar-glass.css (v1.1 液态玻璃, 去 scoped hash) -->

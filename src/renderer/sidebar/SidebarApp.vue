<template>
  <div class="sidebar" :class="{ docked: isDocked }" @mouseenter="onMouseEnter" @mouseleave="onMouseLeave">
    <!-- 图标轨 (垂直/水平自适应) -->
    <div class="rail" :class="{ docked: isDocked }" @dblclick="onRailDblClick">
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
    <div class="panel" v-show="isExpanded && !isDocked" :class="{ expanded: isExpanded }">
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

const RAIL_WIDTH = 52
const PANEL_WIDTH = 380
const EXPANDED_WIDTH = RAIL_WIDTH + PANEL_WIDTH

const isExpanded = ref(false)
const isDocked = ref(false)
const activePanel = ref<string>('')
const imeMode = ref<'cn' | 'en'>('cn')
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

const railItems = computed(() => [
  {
    id: 'ime', panel: 'ime', title: '输入法切换',
    icon: getIconPath('ime'),
    action: () => toggleIme(),
    badge: imeMode.value === 'cn' ? '中' : 'EN',
    badgeClass: imeMode.value === 'cn' ? 'badge-cn' : 'badge-en'
  },
  { id: 'capture', panel: 'capture', title: '区域截图', icon: getIconPath('capture'), action: () => expandPanel('capture') },
  { id: 'annotate', panel: 'capture', title: '批注', icon: getIconPath('annotate'), action: () => expandPanel('capture') },
  { id: 'longshot', panel: 'capture', title: '长截图', icon: getIconPath('longshot'), action: () => expandPanel('capture') },
  { id: 'record', panel: 'capture', title: '录屏', icon: getIconPath('record'), action: () => expandPanel('capture') },
  { id: 'usb', panel: 'usb', title: 'USB 监控', icon: getIconPath('usb'), action: () => expandPanel('usb') },
  { id: 'printer', panel: 'printer', title: '打印机', icon: getIconPath('printer'), action: () => expandPanel('printer') },
  { id: 'taskmgr', panel: 'taskmgr', title: '任务管理器', icon: getIconPath('taskmgr'), action: () => openTaskMgr() },
  { id: 'link', panel: 'links', title: '快捷链接', icon: getIconPath('link'), action: () => expandPanel('links') },
  { id: 'bell', panel: 'reminder', title: '定时提醒', icon: getIconPath('bell'), action: () => expandPanel('reminder') },
  { id: 'lock', panel: 'settings', title: '锁屏/电源', icon: getIconPath('lock'), action: () => expandPanel('settings') },
  { id: 'settings', panel: 'settings', title: '设置', icon: getIconPath('settings'), action: () => expandPanel('settings') }
])

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
  if (isExpanded.value) {
    if ((window as any).__keepSidebarOpen) return
    collapseTimer = setTimeout(() => {
      if ((window as any).__keepSidebarOpen) return
      collapse()
    }, 5000)
  }
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

<style scoped>
.sidebar {
  display: flex;
  height: 100vh;
  width: 100%;
  background: var(--bg);
  box-shadow: -2px 0 8px rgba(0, 0, 0, 0.08);
  overflow: hidden;
}

/* ====== docked 模式: 水平条 ====== */
.sidebar.docked {
  flex-direction: column;
  height: 52px;
}

/* ====== 轨道 ====== */
.rail {
  width: 52px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 8px 0;
  background: var(--bg);
  border-right: 1px solid var(--border);
  z-index: 2;
}

.rail.docked {
  width: 100%;
  height: 52px;
  flex-direction: row;
  align-items: center;
  padding: 0 4px;
  border-right: none;
  border-top: 1px solid var(--border);
  overflow-x: auto;
  overflow-y: hidden;
}

.rail-inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
}

.rail-inner.docked {
  flex-direction: row;
  gap: 2px;
  overflow-y: hidden;
  overflow-x: auto;
  flex: 1;
  min-width: 0;
}

.rail-item {
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-sm);
  position: relative;
  cursor: pointer;
  transition: background var(--anim);
  flex-shrink: 0;
}

.rail-item:hover,
.rail-item.active {
  background: var(--brand-light);
}

.rail-icon {
  width: 22px;
  height: 22px;
  pointer-events: none;
}

.rail-badge {
  position: absolute;
  bottom: 2px;
  right: 2px;
  font-size: 10px;
  font-weight: 600;
  padding: 1px 4px;
  border-radius: 6px;
  color: white;
  line-height: 1;
}

.badge-cn { background: var(--brand); }
.badge-en { background: var(--text-secondary); }

/* ====== 收起/展开 切换按钮 ====== */
.dock-toggle {
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-sm);
  cursor: pointer;
  margin-top: auto;
  margin-bottom: 4px;
  background: var(--bg-hover);
  border: 1px solid var(--border);
  flex-shrink: 0;
  transition: all var(--anim);
}

.dock-toggle:hover {
  background: var(--brand-light);
  border-color: var(--brand);
}

.dock-toggle.docked {
  margin-top: 0;
  margin-bottom: 0;
  margin-left: auto;
  width: 44px;
  height: 44px;
}

/* 与 .rail-icon 同尺寸: 22x22 渲染 24 视框的 stroke-width:2,
   等效线宽 2/24*22 ≈ 1.83px, 与轨道图标笔画完全一致 */
.dock-arrow {
  width: 22px;
  height: 22px;
  display: block;
  color: var(--text-secondary);
}

.dock-toggle:hover .dock-arrow {
  color: var(--brand);
}

/* ====== 面板 ====== */
.panel {
  width: 0;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  background: var(--bg);
  overflow: hidden;
  transition: width 180ms ease;
}

.panel.expanded {
  width: 380px;
}

.panel-header {
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
  border-bottom: 1px solid var(--border);
  flex-shrink: 0;
}

.panel-title {
  font-size: 15px;
  font-weight: 600;
}

.panel-close {
  width: 28px;
  height: 28px;
  font-size: 18px;
  color: var(--text-secondary);
  background: transparent;
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  line-height: 1;
}

.panel-close:hover {
  background: var(--bg-hover);
  color: var(--text);
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 12px 16px;
  min-height: 0;
}
</style>

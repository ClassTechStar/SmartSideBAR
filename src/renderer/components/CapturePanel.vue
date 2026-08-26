<template>
  <div class="panel-content">
    <!-- 录屏中状态 -->
    <div v-if="recording || starting" class="recording-status">
      <div class="rec-indicator">
        <span class="rec-dot"></span>
        <span class="rec-timer">{{ starting ? '启动中...' : formatTime(elapsed) }}</span>
      </div>
      <button class="stop-btn" @click="stopRecording" :disabled="starting">
        <img :src="icon('record')" />
        <span>{{ starting ? '请稍候...' : '停止录屏' }}</span>
      </button>
    </div>

    <!-- 长截图 窗口选择 -->
    <div v-else-if="longshotStep === 'select'" class="longshot-window-select">
      <div class="select-header">
        <img :src="icon('longshot')" />
        <span>选择要截图的窗口</span>
      </div>
      <div class="window-list">
        <div v-for="w in availableWindows" :key="w.handle" class="window-item" @click="confirmWindow(w)">
          <div class="window-title">{{ w.title }}</div>
          <div class="window-meta">{{ w.name }} • PID {{ w.pid }} • {{ w.width }}x{{ w.height }}</div>
        </div>
        <div v-if="availableWindows.length === 0" class="window-empty">
          <p>未检测到可见窗口</p>
          <p class="hint">请确保目标窗口未被最小化</p>
        </div>
      </div>
      <button class="cancel-btn" @click="cancelLongshot">取消</button>
    </div>

    <!-- 长截图 倒计时 -->
    <div v-else-if="longshotStep === 'countdown'" class="longshot-countdown">
      <div class="countdown-overlay">
        <div class="countdown-number">{{ countdownValue }}</div>
        <div class="countdown-hint">请在倒计时结束后保持目标窗口可见</div>
      </div>
    </div>

    <!-- 长截图 进行中 -->
    <div v-else-if="longshotStep === 'running'" class="longshot-status">
      <div class="shot-indicator">
        <span class="shot-dot"></span>
        <span class="shot-text">{{ longshotStatus }}</span>
      </div>
      <div v-if="longshotProgress > 0" class="progress-bar">
        <div class="progress-fill" :style="{ width: longshotProgress + '%' }"></div>
      </div>
      <div class="shot-meta" v-if="longshotFrameCount > 0">
        已截取 {{ longshotFrameCount }} 帧
        <span v-if="longshotTitle"> — {{ longshotTitle }}</span>
      </div>
      <button class="stop-btn" @click="stopLongshot">
        <span>停止长截图</span>
      </button>
    </div>

    <!-- 正常操作 -->
    <div v-else class="action-grid">
      <button class="action-btn" @click="captureRegion">
        <img :src="icon('capture')" />
        <span>区域截图</span>
      </button>
      <button class="action-btn" @click="annotate">
        <img :src="icon('annotate')" />
        <span>批注</span>
      </button>
      <button class="action-btn" @click="startLongshot">
        <img :src="icon('longshot')" />
        <span>长截图</span>
      </button>
      <button class="action-btn" @click="startRecording">
        <img :src="icon('record')" />
        <span>开始录屏</span>
      </button>
    </div>

    <!-- 最近文件 -->
    <div class="recent-section" v-if="recentFiles.length > 0">
      <h4>最近文件</h4>
      <div class="file-list">
        <div class="file-item" v-for="f in recentFiles" :key="f.path" @click="openFile(f.path)">
          <span class="file-type" :class="f.type">{{ f.type === 'video' ? '录屏' : '截图' }}</span>
          <span class="file-name">{{ f.name }}</span>
          <span class="file-time">{{ f.time }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

interface WindowInfo {
  pid: number
  name: string
  title: string
  handle: number
  x: number
  y: number
  width: number
  height: number
}

const recording = ref(false)
const starting = ref(false)
const elapsed = ref(0)

// 长截图状态机: idle | select | countdown | running | done
const longshotStep = ref<'idle' | 'select' | 'countdown' | 'running' | 'done'>('idle')
const longshotStatus = ref('准备中...')
const longshotProgress = ref(0)
const longshotFrameCount = ref(0)
const longshotTitle = ref('')
const availableWindows = ref<WindowInfo[]>([])
const countdownValue = ref(3)
const countdownTimer = ref<ReturnType<typeof setInterval> | null>(null)

const recentFiles = ref<Array<{name: string; path: string; time: string; type: string}>>([])
let unsubStatus: (() => void) | null = null
let unsubLongshot: (() => void) | null = null
let unsubCountdown: (() => void) | null = null

function icon(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

function formatTime(sec: number): string {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}

onMounted(() => {
  // 监听录屏状态变化
  unsubStatus = window.sidekick.recorder.onStatusChanged((status: any) => {
    recording.value = status.recording || false
    starting.value = status.starting || false
    elapsed.value = status.elapsed || 0
    if (!status.recording && !status.starting && status.filepath) {
      const name = status.filepath.split(/[\\/]/).pop() || ''
      recentFiles.value.unshift({
        name,
        path: status.filepath,
        time: new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' }),
        type: 'video'
      })
      if (recentFiles.value.length > 5) recentFiles.value.pop()
    }
  })

  // 监听长截图进度
  unsubLongshot = window.sidekick.longshot.onProgress((data: any) => {
    longshotFrameCount.value = data.frameIndex || 0
    longshotTitle.value = data.title || ''

    if (data.status === 'capturing') {
      longshotStep.value = 'running'
      longshotStatus.value = '正在滚动截图...'
      // 估算进度: 每帧约 1080px, 最多约 20 帧 = 100%
      longshotProgress.value = Math.min(90, ((data.frameIndex || 0) / 20) * 100)
    } else if (data.status === 'stitching') {
      longshotStep.value = 'running'
      longshotStatus.value = '正在拼接图像...'
      longshotProgress.value = 95
    } else if (data.status === 'done') {
      longshotStatus.value = '长截图完成'
      longshotProgress.value = 100
      if (data.filepath) {
        addRecent(data.filepath, 'image')
        // 弹出 toast 通知
        window.sidekick.notification.show({
          title: '长截图完成',
          message: '已保存并复制到剪贴板',
          duration: 4000
        }).catch(() => { /* noop */ })
      }
      setTimeout(() => {
        longshotStep.value = 'idle'
        longshotProgress.value = 0
      }, 2000)
    } else if (data.status === 'error') {
      longshotStep.value = 'idle'
      longshotStatus.value = '长截图失败: ' + (data.error || '未知错误')
      longshotProgress.value = 0
    }
  })

  // 监听倒计时
  unsubCountdown = window.sidekick.longshot.onCountdown((n: number) => {
    countdownValue.value = n
    if (n > 0) {
      longshotStep.value = 'countdown'
    } else if (n === 0) {
      longshotStep.value = 'running'
      longshotStatus.value = '正在滚动截图...'
    }
  })
})

onUnmounted(() => {
  if (unsubStatus) unsubStatus()
  if (unsubLongshot) unsubLongshot()
  if (unsubCountdown) unsubCountdown()
  if (countdownTimer.value) clearInterval(countdownTimer.value)
})

async function captureRegion() {
  try {
    const result = await window.sidekick.capture.region({ mode: 'region' })
    if (result?.success && result.filepath) {
      addRecent(result.filepath, 'image')
    }
  } catch (e) { console.error(e) }
}

async function annotate() {
  try {
    const result = await window.sidekick.capture.annotate({ mode: 'annotate' })
    if (result?.success && result.filepath) {
      addRecent(result.filepath, 'image')
    }
  } catch (e) { console.error(e) }
}

async function startLongshot() {
  if (longshotStep.value !== 'idle') return
  try {
    longshotStep.value = 'select'
    longshotStatus.value = '正在获取窗口列表...'
    const result = await window.sidekick.longshot.selectWindow()
    if (result?.success && result.windows) {
      availableWindows.value = result.windows
    } else {
      availableWindows.value = []
    }
  } catch (e) {
    console.error(e)
    longshotStep.value = 'idle'
  }
}

async function confirmWindow(win: WindowInfo) {
  try {
    longshotStep.value = 'countdown'
    countdownValue.value = 3
    // 主进程会在 3..0 倒计时结束后自动开始
    const result = await window.sidekick.longshot.start({ window: win })
    if (!result) {
      longshotStep.value = 'idle'
      longshotStatus.value = '长截图启动失败'
    }
  } catch (e) {
    console.error(e)
    longshotStep.value = 'idle'
  }
}

function cancelLongshot() {
  longshotStep.value = 'idle'
  availableWindows.value = []
}

async function stopLongshot() {
  try {
    await window.sidekick.longshot.stop()
    longshotStatus.value = '正在停止...'
  } catch (e) { console.error(e) }
}

async function startRecording() {
  if (recording.value || starting.value) return
  try {
    starting.value = true
    const result = await window.sidekick.recorder.start({ fps: 15 })
    if (!result?.success) {
      starting.value = false
      console.error('录屏启动失败:', result?.error)
    }
  } catch (e) {
    starting.value = false
    console.error(e)
  }
}

async function stopRecording() {
  try {
    await window.sidekick.recorder.stop()
  } catch (e) { console.error(e) }
}

function addRecent(path: string, type: string) {
  const name = path.split(/[\\/]/).pop() || ''
  recentFiles.value.unshift({
    name,
    path,
    time: new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' }),
    type
  })
  if (recentFiles.value.length > 5) recentFiles.value.pop()
}

async function openFile(path: string) {
  try {
    await window.sidekick.shell.showItemInFolder(path)
  } catch (e) { console.error(e) }
}
</script>

<style scoped>
.panel-content { display: flex; flex-direction: column; gap: 20px; }

.action-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.action-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 16px;
  background: var(--bg-hover);
  border-radius: var(--radius-md);
  font-size: 13px;
  color: var(--text);
}

.action-btn:hover { background: var(--brand-light); color: var(--brand); }

.action-btn img { width: 20px; height: 20px; }

/* 窗口选择 */
.longshot-window-select {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.select-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 600;
  color: var(--brand);
}

.select-header img { width: 20px; height: 20px; }

.window-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-height: 280px;
  overflow-y: auto;
}

.window-item {
  padding: 10px 12px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  cursor: pointer;
  border: 1px solid transparent;
  transition: all var(--anim);
}

.window-item:hover {
  border-color: var(--brand);
  background: var(--brand-light);
}

.window-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.window-meta {
  font-size: 11px;
  color: var(--text-secondary);
  margin-top: 2px;
}

.window-empty {
  text-align: center;
  padding: 24px 0;
  color: var(--text-secondary);
}

.window-empty .hint {
  font-size: 11px;
  margin-top: 4px;
}

.cancel-btn {
  padding: 8px 16px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  font-size: 13px;
  color: var(--text-secondary);
  text-align: center;
}

.cancel-btn:hover { color: var(--text); background: var(--border); }

/* 倒计时 */
.longshot-countdown {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
}

.countdown-overlay {
  text-align: center;
}

.countdown-number {
  font-size: 72px;
  font-weight: 700;
  color: var(--brand);
  line-height: 1;
  animation: pulseScale 1s ease-in-out;
}

.countdown-hint {
  font-size: 13px;
  color: var(--text-secondary);
  margin-top: 16px;
}

@keyframes pulseScale {
  0% { transform: scale(0.5); opacity: 0; }
  50% { transform: scale(1.1); opacity: 1; }
  100% { transform: scale(1); opacity: 1; }
}

/* 长截图进行中 */
.longshot-status {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  background: rgba(43, 110, 224, 0.08);
  border-radius: var(--radius-md);
  border: 1px solid rgba(43, 110, 224, 0.2);
}

.shot-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
}

.shot-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--brand);
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.3; }
}

.shot-text {
  font-size: 15px;
  font-weight: 600;
  color: var(--brand);
}

.progress-bar {
  width: 100%;
  height: 6px;
  background: var(--border);
  border-radius: 3px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: var(--brand);
  border-radius: 3px;
  transition: width 300ms ease;
}

.shot-meta {
  font-size: 12px;
  color: var(--text-secondary);
}

.stop-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 10px;
  background: #e74c3c;
  color: #fff;
  border-radius: var(--radius-sm);
  font-size: 14px;
}

.stop-btn:hover:not(:disabled) { background: #c0392b; }

.stop-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.stop-btn img { width: 20px; height: 20px; filter: brightness(0) invert(1); }

/* 录屏状态 */
.recording-status {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  background: rgba(231, 76, 60, 0.08);
  border-radius: var(--radius-md);
  border: 1px solid rgba(231, 76, 60, 0.2);
}

.rec-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
}

.rec-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #e74c3c;
  animation: pulse 1.5s ease-in-out infinite;
}

.rec-timer {
  font-size: 20px;
  font-weight: 600;
  color: #e74c3c;
  font-variant-numeric: tabular-nums;
}

.recent-section h4 {
  font-size: 13px;
  margin-bottom: 8px;
  color: var(--text-secondary);
}

.file-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.file-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  cursor: pointer;
}

.file-item:hover { background: var(--brand-light); }

.file-type {
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 11px;
  color: #fff;
}

.file-type.image { background: var(--brand); }
.file-type.video { background: #e74c3c; }

.file-name {
  flex: 1;
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-time {
  font-size: 11px;
  color: var(--text-disabled);
}
</style>

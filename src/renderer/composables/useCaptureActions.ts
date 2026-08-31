// composables/useCaptureActions.ts - 截图/录屏/长截图操作逻辑 (P2-10 拆分自 CapturePanel.vue)

import { ref, onMounted, onUnmounted } from 'vue'

export interface WindowInfo {
  pid: number
  name: string
  title: string
  handle: number
  x: number
  y: number
  width: number
  height: number
}

export interface RecentFile {
  name: string
  path: string
  time: string
  type: string
}

export function useCaptureActions() {
  // ---- 录屏状态 ----
  const recording = ref(false)
  const starting = ref(false)
  const elapsed = ref(0)

  // ---- 长截图状态机 ----
  const longshotStep = ref<'idle' | 'select' | 'countdown' | 'running' | 'done'>('idle')
  const longshotStatus = ref('准备中...')
  const longshotProgress = ref(0)
  const longshotFrameCount = ref(0)
  const longshotTitle = ref('')
  const availableWindows = ref<WindowInfo[]>([])
  const countdownValue = ref(3)

  // ---- 最近文件 ----
  const recentFiles = ref<RecentFile[]>([])

  function formatTime(sec: number): string {
    const m = Math.floor(sec / 60)
    const s = sec % 60
    return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
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

  // ---- 录屏 ----
  async function startRecording() {
    if (recording.value || starting.value) return
    try {
      starting.value = true
      const result = await window.sidekick.recorder.start()
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
    try { await window.sidekick.recorder.stop() }
    catch (e) { console.error(e) }
  }

  // ---- 截图/批注 ----
  async function captureRegion() {
    try {
      const result = await window.sidekick.capture.region({ mode: 'region' })
      if (result?.success && result.filepath) addRecent(result.filepath, 'image')
    } catch (e) { console.error(e) }
  }

  async function annotate() {
    try {
      const result = await window.sidekick.capture.annotate({ mode: 'annotate' })
      if (result?.success && result.filepath) addRecent(result.filepath, 'image')
    } catch (e) { console.error(e) }
  }

  // ---- 长截图 ----
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

  // ---- 生命周期 ----
  let unsubStatus: (() => void) | null = null
  let unsubLongshot: (() => void) | null = null
  let unsubCountdown: (() => void) | null = null

  onMounted(() => {
    unsubStatus = window.sidekick.recorder.onStatusChanged((status: any) => {
      recording.value = status.recording || false
      starting.value = status.starting || false
      elapsed.value = status.elapsed || 0
      if (!status.recording && !status.starting && status.filepath) {
        addRecent(status.filepath, 'video')
      }
    })

    unsubLongshot = window.sidekick.longshot.onProgress((data: any) => {
      longshotFrameCount.value = data.frameIndex || 0
      longshotTitle.value = data.title || ''
      if (data.status === 'capturing') {
        longshotStep.value = 'running'
        longshotStatus.value = '正在滚动截图...'
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
          window.sidekick.notification.show({
            title: '长截图完成',
            message: '已保存并复制到剪贴板',
            duration: 4000
          }).catch(() => {})
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

    unsubCountdown = window.sidekick.longshot.onCountdown((n: number) => {
      countdownValue.value = n
      if (n > 0) longshotStep.value = 'countdown'
      else if (n === 0) {
        longshotStep.value = 'running'
        longshotStatus.value = '正在滚动截图...'
      }
    })
  })

  onUnmounted(() => {
    if (unsubStatus) unsubStatus()
    if (unsubLongshot) unsubLongshot()
    if (unsubCountdown) unsubCountdown()
  })

  return {
    recording, starting, elapsed,
    longshotStep, longshotStatus, longshotProgress, longshotFrameCount, longshotTitle,
    availableWindows, countdownValue,
    recentFiles,
    formatTime, addRecent,
    captureRegion, annotate,
    startRecording, stopRecording,
    startLongshot, confirmWindow, cancelLongshot, stopLongshot
  }
}

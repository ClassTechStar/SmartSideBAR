<template>
  <div class="recorder-root"></div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'

let mediaRecorder: MediaRecorder | null = null
let chunks: Blob[] = []
let stream: MediaStream | null = null
let unsubStart: (() => void) | null = null
let unsubStop: (() => void) | null = null

onMounted(() => {
  // 通知主进程页面已就绪
  window.sidekick.recorder.ready()

  // 监听开始指令
  unsubStart = window.sidekick.recorder.onPageStart(async (opts: any) => {
    try {
      const fps = opts?.fps || 15
      const mic = opts?.mic || false

      // 调用 getDisplayMedia (主进程已设置 setDisplayMediaRequestHandler)
      stream = await navigator.mediaDevices.getDisplayMedia({
        video: { frameRate: { ideal: fps, max: 30 } },
        audio: mic
      })

      // 选择最佳 mimeType
      const mimeType = getSupportedMimeType()

      mediaRecorder = new MediaRecorder(stream, {
        mimeType,
        videoBitsPerSecond: 2500000
      })

      chunks = []

      mediaRecorder.ondataavailable = (e: BlobEvent) => {
        if (e.data.size > 0) {
          chunks.push(e.data)
        }
      }

      mediaRecorder.onstop = () => {
        const blob = new Blob(chunks, { type: mimeType })
        blob.arrayBuffer().then((buffer) => {
          window.sidekick.recorder.sendComplete(buffer, mimeType)
          cleanup()
        }).catch((err) => {
          console.error('Failed to convert blob:', err)
          window.sidekick.recorder.sendComplete(new ArrayBuffer(0), mimeType)
          cleanup()
        })
      }

      mediaRecorder.onerror = (e: any) => {
        console.error('MediaRecorder error:', e)
        window.sidekick.recorder.sendComplete(new ArrayBuffer(0), mimeType)
        cleanup()
      }

      // 用户可能在浏览器层面停止了屏幕共享
      stream.getVideoTracks().forEach((track) => {
        track.onended = () => {
          if (mediaRecorder && mediaRecorder.state !== 'inactive') {
            mediaRecorder.stop()
          }
        }
      })

      // 开始录制, 每 1 秒触发一次 dataavailable
      mediaRecorder.start(1000)

      // 通知主进程已开始
      window.sidekick.recorder.sendStarted()

      console.log('[Recorder] Started', { fps, mimeType })
    } catch (e: any) {
      console.error('[Recorder] Failed to start:', e)
      // 通知主进程失败 (发送空数据)
      window.sidekick.recorder.sendComplete(new ArrayBuffer(0), 'video/webm')
      cleanup()
    }
  })

  // 监听停止指令
  unsubStop = window.sidekick.recorder.onPageStop(() => {
    if (mediaRecorder && mediaRecorder.state !== 'inactive') {
      mediaRecorder.stop()
    }
    console.log('[Recorder] Stopping...')
  })
})

function getSupportedMimeType(): string {
  const candidates = [
    'video/webm;codecs=vp9',
    'video/webm;codecs=vp8',
    'video/webm',
    'video/mp4'
  ]
  for (const t of candidates) {
    if (MediaRecorder.isTypeSupported(t)) return t
  }
  return 'video/webm'
}

function cleanup() {
  if (stream) {
    stream.getTracks().forEach((t) => t.stop())
    stream = null
  }
  mediaRecorder = null
  chunks = []
}

onUnmounted(() => {
  if (unsubStart) unsubStart()
  if (unsubStop) unsubStop()
  cleanup()
})
</script>

<style scoped>
.recorder-root {
  width: 1px;
  height: 1px;
  overflow: hidden;
}
</style>

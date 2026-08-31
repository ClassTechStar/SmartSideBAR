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

    <!-- 长截图 窗口选择 (P2-10: 子组件) -->
    <LongshotWindowSelect
      v-else-if="longshotStep === 'select'"
      :windows="availableWindows"
      @confirm="confirmWindow"
      @cancel="cancelLongshot"
    />

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

    <!-- 最近文件 (P2-10: 子组件) -->
    <RecentFilesList :files="recentFiles" @open="openFile" />
  </div>
</template>

<script setup lang="ts">
import LongshotWindowSelect from '../capture/LongshotWindowSelect.vue'
import RecentFilesList from '../capture/RecentFilesList.vue'
import { useCaptureActions } from '../composables/useCaptureActions'

function icon(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

function openFile(path: string) {
  window.sidekick.shell.showItemInFolder(path).catch(console.error)
}

const {
  recording, starting, elapsed,
  longshotStep, longshotStatus, longshotProgress, longshotFrameCount, longshotTitle,
  availableWindows, countdownValue,
  recentFiles, formatTime,
  captureRegion, annotate,
  startRecording, stopRecording,
  startLongshot, confirmWindow, cancelLongshot, stopLongshot
} = useCaptureActions()
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

/* 倒计时 */
.longshot-countdown {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
}

.countdown-overlay { text-align: center; }

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
.stop-btn:disabled { opacity: 0.6; cursor: not-allowed; }
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
</style>

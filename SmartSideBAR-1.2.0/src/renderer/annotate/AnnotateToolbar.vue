<template>
  <div class="toolbar">
    <div class="tool-group">
      <button
        v-for="t in tools"
        :key="t.id"
        class="tool-btn"
        :class="{ active: currentTool === t.id }"
        @click="emit('update:tool', t.id)"
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
        @click="emit('update:color', c)"
      />
    </div>

    <div class="divider"></div>

    <div class="size-group">
      <button
        v-for="s in sizes"
        :key="s.value"
        class="size-btn"
        :class="{ active: currentSize === s.value }"
        @click="emit('update:size', s.value)"
        :title="s.label"
      >
        <span class="size-dot" :style="{ width: s.value + 'px', height: s.value + 'px' }"></span>
      </button>
    </div>

    <div class="divider"></div>

    <div v-if="isTransparent" class="bg-control">
      <button
        class="bg-toggle"
        :class="{ active: bgOpacity > 0 }"
        @click="emit('toggle-bg')"
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
        :value="bgOpacity"
        @input="emit('update:bgOpacity', Number(($event.target as HTMLInputElement).value))"
      />
    </div>

    <div class="divider" v-if="isTransparent"></div>

    <button class="action-btn undo" @click="emit('undo')" :disabled="!canUndo">
      <span>撤销</span>
    </button>
    <button class="action-btn save" @click="emit('save-only')" title="仅保存笔迹 (透明背景)">
      <span>仅笔迹</span>
    </button>
    <button class="action-btn save-bg" @click="emit('save-bg')" :disabled="!hasBackground" :title="hasBackground ? '保存笔迹+屏幕截图' : '正在获取屏幕截图...'">
      <span>含截图</span>
    </button>
    <button class="action-btn cancel" @click="emit('cancel')">
      <span>取消</span>
    </button>
  </div>
</template>

<script setup lang="ts">
import type { AnnotateTool } from '../composables/useAnnotate'
import { ANNOTATE_TOOLS, ANNOTATE_COLORS, ANNOTATE_SIZES } from '../composables/useAnnotate'

defineProps<{
  currentTool: AnnotateTool
  currentColor: string
  currentSize: number
  bgOpacity: number
  isTransparent: boolean
  canUndo: boolean
  hasBackground: boolean
}>()

const emit = defineEmits<{
  (e: 'update:tool', v: AnnotateTool): void
  (e: 'update:color', v: string): void
  (e: 'update:size', v: number): void
  (e: 'update:bgOpacity', v: number): void
  (e: 'toggle-bg'): void
  (e: 'undo'): void
  (e: 'save-only'): void
  (e: 'save-bg'): void
  (e: 'cancel'): void
}>()

const tools = ANNOTATE_TOOLS
const colors = ANNOTATE_COLORS
const sizes = ANNOTATE_SIZES
</script>

<style scoped>
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

.tool-group, .color-group, .size-group { display: flex; gap: 4px; }

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

.tool-btn:hover { background: rgba(255, 255, 255, 0.1); color: #fff; }
.tool-btn.active { background: var(--brand, #2B6EE0); color: #fff; border-color: rgba(255, 255, 255, 0.3); }

.color-btn {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  border: 2px solid rgba(255, 255, 255, 0.2);
  transition: all 120ms ease;
}
.color-btn:hover { transform: scale(1.15); }
.color-btn.active { border-color: #fff; box-shadow: 0 0 0 2px rgba(43, 110, 224, 0.5); }

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
.size-btn:hover { background: rgba(255, 255, 255, 0.1); }
.size-btn.active { background: rgba(43, 110, 224, 0.3); border-color: rgba(255, 255, 255, 0.2); }

.size-dot { background: #ccc; border-radius: 50%; }

.bg-control { display: flex; align-items: center; gap: 8px; }

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
.bg-toggle:hover { background: rgba(255, 255, 255, 0.1); color: #fff; }
.bg-toggle.active { background: rgba(43, 110, 224, 0.4); border-color: rgba(43, 110, 224, 0.6); color: #fff; }

.bg-slider { width: 60px; height: 4px; accent-color: #2B6EE0; cursor: pointer; }

.action-btn {
  padding: 6px 14px;
  border-radius: 8px;
  font-size: 13px;
  color: #ccc;
  background: transparent;
  border: 1px solid transparent;
  transition: all 120ms ease;
}
.action-btn:hover { background: rgba(255, 255, 255, 0.1); color: #fff; }
.action-btn.undo:disabled { opacity: 0.3; cursor: not-allowed; }
.action-btn.save { background: #27ae60; color: #fff; }
.action-btn.save:hover { background: #229954; }
.action-btn.save-bg { background: #2B6EE0; color: #fff; }
.action-btn.save-bg:hover { background: #1a5dc4; }
.action-btn.save-bg:disabled { opacity: 0.4; cursor: not-allowed; }
.action-btn.cancel { background: rgba(231, 76, 60, 0.8); color: #fff; }
.action-btn.cancel:hover { background: #c0392b; }
</style>

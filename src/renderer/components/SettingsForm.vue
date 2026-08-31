<template>
  <div class="settings-form">
    <!-- 通用设置 -->
    <section class="settings-section">
      <h3>通用</h3>
      <div class="setting-row">
        <span>开机自启动</span>
        <button class="toggle" :class="{ on: autoLaunch }" @click="toggleAutoLaunch">
          <span class="toggle-knob"></span>
        </button>
      </div>
      <div class="setting-row">
        <span>侧边栏位置</span>
        <select v-model="sidebarSide">
          <option value="right">右侧</option>
          <option value="left">左侧</option>
        </select>
      </div>
    </section>

    <!-- 输入法 -->
    <section class="settings-section">
      <h3>输入法</h3>
      <div class="setting-row">
        <span>槽位 1</span>
        <input v-model="imeSlot1" class="text-input" />
      </div>
      <div class="setting-row">
        <span>槽位 2</span>
        <input v-model="imeSlot2" class="text-input" />
      </div>
    </section>

    <!-- 截图 -->
    <section class="settings-section">
      <h3>截图与批注</h3>
      <div class="setting-row">
        <span>截图快捷键</span>
        <input v-model="captureHotkey" class="text-input" />
      </div>
      <div class="setting-row">
        <span>保存格式</span>
        <select v-model="captureFormat">
          <option value="PNG">PNG</option>
          <option value="JPG">JPG</option>
        </select>
      </div>
      <div class="setting-row">
        <span>保存目录</span>
        <input v-model="captureDir" class="text-input" />
      </div>
    </section>

    <!-- 录屏 -->
    <section class="settings-section">
      <h3>录屏</h3>
      <div class="setting-row">
        <span>帧率 (fps)</span>
        <select v-model.number="recorderFps">
          <option :value="15">15 (省资源)</option>
          <option :value="24">24 (流畅)</option>
          <option :value="30">30 (高清)</option>
          <option :value="60">60 (超清)</option>
        </select>
      </div>
      <div class="setting-row">
        <span>录制麦克风</span>
        <button class="toggle" :class="{ on: recorderMic }" @click="recorderMic = !recorderMic">
          <span class="toggle-knob"></span>
        </button>
      </div>
    </section>

    <!-- USB -->
    <section class="settings-section">
      <h3>USB 监控</h3>
      <div class="setting-row">
        <span>启用 USB 监控</span>
        <button class="toggle" :class="{ on: usbEnabled }" @click="usbEnabled = !usbEnabled">
          <span class="toggle-knob"></span>
        </button>
      </div>
    </section>

    <!-- 打印机 -->
    <section class="settings-section">
      <h3>打印机</h3>
      <div class="setting-row">
        <span>轮询间隔 (秒)</span>
        <input v-model.number="printerInterval" type="number" min="5" max="60" class="text-input" />
      </div>
    </section>

    <!-- 关于 -->
    <section class="settings-section">
      <h3>关于</h3>
      <div class="about-info">
        <p>希沃侧边快捷键工具 v1.1.0</p>
        <p class="muted">Electron + Vue 3</p>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
// P1-6: 统一设置表单 —— 原 SettingsApp(窗口) 与 SettingsPanel(侧边) 两套字段漂移,
// 现抽取为单一实现, 供设置窗口嵌入; 侧边栏面板保留快捷入口 + 诊断, 不再重复表单字段。
import { ref, onMounted } from 'vue'

const emit = defineEmits<{ (e: 'saved'): void }>()

const autoLaunch = ref(false)
const sidebarSide = ref('right')
const imeSlot1 = ref('Microsoft Pinyin')
const imeSlot2 = ref('US')
const captureHotkey = ref('Ctrl+Shift+A')
const captureFormat = ref('PNG')
const captureDir = ref('')
const recorderFps = ref(15)
const recorderMic = ref(false)
const usbEnabled = ref(true)
const printerInterval = ref(10)

async function loadConfig() {
  try {
    const cfg = await window.sidekick.config.get()
    sidebarSide.value = cfg.display.sidebarSide
    imeSlot1.value = cfg.ime.slot1
    imeSlot2.value = cfg.ime.slot2
    captureHotkey.value = cfg.capture.hotkey
    captureFormat.value = cfg.capture.format
    captureDir.value = cfg.capture.dir
    recorderFps.value = cfg.recorder?.fps || 15
    recorderMic.value = cfg.recorder?.mic ?? false
    usbEnabled.value = cfg.usb.enabled
    printerInterval.value = cfg.printer.pollIntervalSec
  } catch (e) {
    console.error('Load config failed:', e)
  }
  // P1-6/E3: autoLaunch 读取系统登录项真实状态
  try {
    autoLaunch.value = await window.sidekick.power.getAutoLaunch()
  } catch (e) {
    console.error('Get autoLaunch failed:', e)
  }
}

async function toggleAutoLaunch() {
  autoLaunch.value = !autoLaunch.value
  await window.sidekick.power.setAutoLaunch(autoLaunch.value)
}

async function save() {
  try {
    await window.sidekick.config.set('display.sidebarSide', sidebarSide.value)
    await window.sidekick.config.set('ime.slot1', imeSlot1.value)
    await window.sidekick.config.set('ime.slot2', imeSlot2.value)
    await window.sidekick.config.set('capture.hotkey', captureHotkey.value)
    await window.sidekick.config.set('capture.format', captureFormat.value)
    await window.sidekick.config.set('capture.dir', captureDir.value)
    await window.sidekick.config.set('recorder.fps', recorderFps.value)
    await window.sidekick.config.set('recorder.mic', recorderMic.value)
    await window.sidekick.config.set('usb.enabled', usbEnabled.value)
    await window.sidekick.config.set('printer.pollIntervalSec', printerInterval.value)
    emit('saved')
  } catch (e) {
    console.error('Save settings failed:', e)
    throw e
  }
}

defineExpose({ save })

onMounted(loadConfig)
</script>

<style scoped>
.settings-section {
  margin-bottom: 24px;
  background: white;
  border-radius: var(--radius-md);
  padding: 16px 20px;
}

.settings-section h3 {
  font-size: 14px;
  margin-bottom: 12px;
  color: var(--brand);
}

.setting-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 0;
}

.text-input, select {
  padding: 6px 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  font-size: 13px;
  font-family: inherit;
  width: 200px;
}

.toggle {
  width: 44px;
  height: 20px;
  border-radius: 12px;
  background: #ccc;
  position: relative;
  padding: 0;
  transition: background var(--anim);
}

.toggle.on { background: var(--brand); }

.toggle-knob {
  position: absolute;
  top: 2px;
  left: 2px;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: white;
  transition: transform var(--anim);
}

.toggle.on .toggle-knob { transform: translateX(24px); }

.about-info p { margin: 4px 0; }
.muted { color: var(--text-secondary); font-size: 12px; }
</style>

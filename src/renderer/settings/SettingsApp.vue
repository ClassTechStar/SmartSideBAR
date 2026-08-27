<template>
  <div class="settings-app">
    <div class="settings-header">
      <h1>设置</h1>
    </div>
    <div class="settings-body">
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
          <p>希沃侧边快捷键工具 v2.0.0</p>
          <p class="muted">Electron + Vue 3</p>
        </div>
      </section>
    </div>

    <div class="settings-footer">
      <button class="btn-save" @click="save">保存设置</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'

const autoLaunch = ref(false)
const sidebarSide = ref('right')
const imeSlot1 = ref('Microsoft Pinyin')
const imeSlot2 = ref('US')
const captureHotkey = ref('Ctrl+Shift+A')
const captureFormat = ref('PNG')
const captureDir = ref('')
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
    usbEnabled.value = cfg.usb.enabled
    printerInterval.value = cfg.printer.pollIntervalSec
  } catch (e) {
    console.error('Load config failed:', e)
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
    await window.sidekick.config.set('usb.enabled', usbEnabled.value)
    await window.sidekick.config.set('printer.pollIntervalSec', printerInterval.value)
    alert('设置已保存')
  } catch (e) {
    alert('保存失败: ' + e)
  }
}

onMounted(loadConfig)
</script>

<style scoped>
.settings-app {
  width: 100%;
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: #f5f7fa;
}

.settings-header {
  padding: 16px 24px;
  background: white;
  border-bottom: 1px solid var(--border);
}

.settings-header h1 { font-size: 18px; }

.settings-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px 24px;
}

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

.settings-footer {
  padding: 16px 24px;
  background: white;
  border-top: 1px solid var(--border);
  display: flex;
  justify-content: flex-end;
}

.btn-save {
  padding: 8px 32px;
  background: var(--brand);
  color: white;
  border-radius: var(--radius-sm);
  font-size: 14px;
  font-weight: 500;
}

.btn-save:hover { background: var(--brand-hover); }
</style>

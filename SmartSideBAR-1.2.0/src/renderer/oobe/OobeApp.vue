<template>
  <div class="oobe">
    <!-- 进度指示 -->
    <div class="oobe-progress">
      <div v-for="(s, i) in steps" :key="s.id" class="step-dot" :class="{ active: i === currentStep, done: i < currentStep }">
        <span class="step-num">{{ i < currentStep ? '✓' : i + 1 }}</span>
        <span class="step-label">{{ s.title }}</span>
      </div>
    </div>

    <!-- 步骤内容 -->
    <div class="oobe-body">
      <!-- Step 0: 欢迎 -->
      <div v-if="currentStep === 0" class="step-content">
        <img :src="getIconPath('sidebar')" class="welcome-icon" />
        <h1>欢迎使用希沃侧边快捷键工具</h1>
        <p>一键管理输入法、截图、USB、打印机等常用教学工具</p>
        <p class="hint">本向导将在 3 分钟内完成环境检测与偏好设置</p>
      </div>

      <!-- Step 1: 角色选择 -->
      <div v-if="currentStep === 1" class="step-content">
        <h2>请选择您的角色</h2>
        <div class="role-cards">
          <div class="role-card" :class="{ selected: role === 'teacher' }" @click="role = 'teacher'">
            <img :src="getIconPath('bell')" class="role-icon" />
            <span>教师</span>
          </div>
          <div class="role-card" :class="{ selected: role === 'admin' }" @click="role = 'admin'">
            <img :src="getIconPath('settings')" class="role-icon" />
            <span>管理员</span>
          </div>
        </div>
      </div>

      <!-- Step 2: 环境检测 -->
      <div v-if="currentStep === 2" class="step-content">
        <h2>环境检测</h2>
        <div class="env-list">
          <div class="env-item" v-for="item in envChecklist" :key="item.key">
            <span class="env-name">{{ item.label }}</span>
            <span class="env-status" :class="item.status">{{ item.text }}</span>
          </div>
        </div>
      </div>

      <!-- Step 3: 功能偏好 -->
      <div v-if="currentStep === 3" class="step-content">
        <h2>选择需要启用的功能</h2>
        <div class="pref-list">
          <label class="pref-item" v-for="p in prefs" :key="p.key">
            <input type="checkbox" v-model="p.enabled" />
            <img :src="getIconPath(p.icon)" class="pref-icon" />
            <span>{{ p.label }}</span>
          </label>
        </div>
      </div>

      <!-- Step 4: 开机自启 -->
      <div v-if="currentStep === 4" class="step-content">
        <h2>开机自启动</h2>
        <p>建议启用,确保每次开机后侧边栏自动就绪</p>
        <div class="toggle-row">
          <span>开机自动启动</span>
          <button class="toggle" :class="{ on: autoLaunch }" @click="autoLaunch = !autoLaunch">
            <span class="toggle-knob"></span>
          </button>
        </div>
      </div>

      <!-- Step 5: 完成 -->
      <div v-if="currentStep === 5" class="step-content">
        <img :src="getIconPath('bell')" class="welcome-icon" />
        <h1>设置完成!</h1>
        <p>侧边栏已就绪，点击屏幕边缘的图标即可使用各项功能</p>
        <p class="hint">您可以随时在设置中修改配置</p>
      </div>
    </div>

    <!-- 导航按钮 -->
    <div class="oobe-nav">
      <!-- 保留上一步的布局槽位，避免首步时其余按钮横向跳动 -->
      <button
        class="btn-secondary"
        :class="{ 'nav-placeholder': currentStep === 0 }"
        :disabled="currentStep === 0"
        :aria-hidden="currentStep === 0"
        @click="prevStep"
      >
上一步
</button>
      <button v-if="currentStep < 5" class="btn-primary" @click="nextStep">下一步</button>
      <button v-if="currentStep === 5" class="btn-primary" @click="finish">开始使用</button>
      <button class="btn-skip" @click="skip">跳过</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'

const currentStep = ref(0)
const role = ref<'teacher' | 'admin' | null>(null)
const autoLaunch = ref(true)

const steps = [
  { id: 0, title: '欢迎' },
  { id: 1, title: '角色' },
  { id: 2, title: '检测' },
  { id: 3, title: '功能' },
  { id: 4, title: '自启' },
  { id: 5, title: '完成' }
]

const envChecklist = ref([
  { key: 'screen', label: '屏幕分辨率', status: 'ok', text: '检测中...' },
  { key: 'touch', label: '触控能力', status: 'ok', text: '检测中...' },
  { key: 'ime', label: '输入法服务', status: 'ok', text: '检测中...' },
  { key: 'printer', label: '打印机', status: 'ok', text: '检测中...' },
  { key: 'usb', label: 'USB 监控', status: 'ok', text: '检测中...' }
])

const prefs = ref([
  { key: 'ime', label: '输入法切换', icon: 'ime', enabled: true },
  { key: 'usb', label: 'USB 监控', icon: 'usb', enabled: true },
  { key: 'shot', label: '截图与批注', icon: 'capture', enabled: true },
  { key: 'recorder', label: '录屏', icon: 'record', enabled: false },
  { key: 'printer', label: '打印机监控', icon: 'printer', enabled: true }
])

function getIconPath(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

function prevStep() {
  if (currentStep.value > 0) currentStep.value--
}

function nextStep() {
  if (currentStep.value < 5) currentStep.value++
}

async function skip() {
  await window.sidekick.oobe.setState({ completed: false, skipped: true, lastStepIndex: currentStep.value })
  await window.sidekick.oobe.closeAndOpenMain()
}

async function finish() {
  const prefMap: any = {}
  for (const p of prefs.value) {
    prefMap[p.key] = p.enabled
  }

  // P2-3: 让 prefs / role 真正生效 —— 将未启用的功能映射为策略禁用模块,
  // 由 SidebarApp 的 railItems 过滤 + 主进程服务启动过滤 (P2-1) 消费。
  const disabledModules: string[] = []
  if (prefMap.ime === false) disabledModules.push('ime')
  if (prefMap.usb === false) disabledModules.push('usb')
  if (prefMap.printer === false) disabledModules.push('printer')
  // capture 模块包含区域截图/批注/长截图/录屏; 仅当「截图」与「录屏」都被取消时才禁用
  if (prefMap.shot === false && prefMap.recorder === false) disabledModules.push('capture')
  // role 预设: 教师默认隐藏管理类入口 (任务管理器), 管理员保留全部
  if (role.value === 'teacher') {
    disabledModules.push('taskmgr')
  }

  await window.sidekick.oobe.setState({
    completed: true,
    completedAt: new Date().toISOString(),
    skipped: false,
    role: role.value,
    lastStepIndex: 5,
    prefs: prefMap
  })

  // 策略禁用模块单独写入 policy 键 (与 oobe 键分离)
  await window.sidekick.config.set('policy', { disabledModules })

  if (autoLaunch.value) {
    await window.sidekick.power.setAutoLaunch(true)
  }

  await window.sidekick.oobe.closeAndOpenMain()
}

onMounted(async () => {
  // 获取已有状态
  try {
    const state = await window.sidekick.oobe.getState()
    if (state.lastStepIndex > 0) {
      currentStep.value = state.lastStepIndex
    }
    if (state.role) role.value = state.role
  } catch { /* 首次运行 */ }

  // 环境检测
  setTimeout(async () => {
    // 1. 屏幕分辨率
    try {
      const displays = await window.sidekick.display.list()
      if (displays && displays.length > 0) {
        const d = displays[0]
        envChecklist.value[0].text = `${d.workArea.width}×${d.workArea.height} @ ${d.scaleFactor}x`
        envChecklist.value[0].status = 'ok'
      }
    } catch {
      envChecklist.value[0].text = '检测失败'
      envChecklist.value[0].status = 'warn'
    }

    // 2. 触控能力 (P1-12 真实化)
    try {
      const hasTouch = 'ontouchstart' in window ||
        (navigator.maxTouchPoints && navigator.maxTouchPoints > 0) ||
        window.matchMedia('((pointer: coarse)').matches
      envChecklist.value[1].text = hasTouch ? '支持触控' : '仅鼠标/键盘'
      envChecklist.value[1].status = hasTouch ? 'ok' : 'warn'
    } catch {
      envChecklist.value[1].text = '检测失败'
      envChecklist.value[1].status = 'warn'
    }

    // 3. 输入法服务
    try {
      const ime = await window.sidekick.ime.getState()
      envChecklist.value[2].text = ime.mode === 'cn' ? '中文' : '英文'
      envChecklist.value[2].status = 'ok'
    } catch {
      envChecklist.value[2].text = '降级模式'
      envChecklist.value[2].status = 'warn'
    }

    // 4. 打印机 (P1-12 真实化)
    try {
      const printers = await window.sidekick.printer.getStatus()
      if (printers && printers.length > 0) {
        envChecklist.value[3].text = `已发现 ${printers.length} 台`
        envChecklist.value[3].status = 'ok'
      } else {
        envChecklist.value[3].text = '未检测到打印机'
        envChecklist.value[3].status = 'warn'
      }
    } catch {
      envChecklist.value[3].text = '检测失败'
      envChecklist.value[3].status = 'warn'
    }

    // 5. USB 监控 (P1-12 真实化)
    try {
      const usbResult = await window.sidekick.usb.scan()
      if (usbResult?.success) {
        const count = usbResult.drives?.length || 0
        envChecklist.value[4].text = count > 0 ? `已发现 ${count} 个设备` : '当前无 USB 设备'
        envChecklist.value[4].status = 'ok'
      } else {
        envChecklist.value[4].text = '监控通道异常'
        envChecklist.value[4].status = 'warn'
      }
    } catch {
      envChecklist.value[4].text = '检测失败'
      envChecklist.value[4].status = 'warn'
    }
  }, 500)
})
</script>

<style scoped>
.oobe {
  width: 100%;
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: #f5f7fa;
}

.oobe-progress {
  display: flex;
  justify-content: center;
  padding: 24px 32px;
  gap: 24px;
  background: white;
  border-bottom: 1px solid var(--border);
}

.step-dot {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.step-num {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: #ddd;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 600;
  transition: all var(--anim);
}

.step-dot.active .step-num { background: var(--brand); }
.step-dot.done .step-num { background: var(--success); }

.step-label { font-size: 11px; color: var(--text-secondary); }
.step-dot.active .step-label { color: var(--brand); font-weight: 600; }

.oobe-body {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 32px;
}

.step-content {
  text-align: center;
  max-width: 480px;
}

.step-content h1 { font-size: 24px; margin: 16px 0 8px; }
.step-content h2 { font-size: 18px; margin-bottom: 20px; }
.step-content p { color: var(--text-secondary); margin: 4px 0; }
.step-content .hint { font-size: 12px; margin-top: 12px; }

.welcome-icon { width: 64px; height: 64px; }

.role-cards {
  display: flex;
  gap: 24px;
  justify-content: center;
  margin-top: 24px;
}

.role-card {
  width: 140px;
  height: 140px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  border: 2px solid var(--border);
  border-radius: var(--radius-lg);
  cursor: pointer;
  transition: all var(--anim);
  background: white;
}

.role-card.selected { border-color: var(--brand); background: var(--brand-light); }
.role-card:hover { border-color: var(--brand); }

.role-icon { width: 40px; height: 40px; }

.env-list, .pref-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-top: 16px;
  text-align: left;
}

.env-item {
  display: flex;
  justify-content: space-between;
  padding: 10px 16px;
  background: white;
  border-radius: var(--radius-sm);
}

.env-status.ok { color: var(--success); }
.env-status.warn { color: var(--warn); }
.env-status.error { color: var(--danger); }

.pref-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 16px;
  background: white;
  border-radius: var(--radius-sm);
  cursor: pointer;
}

.pref-icon { width: 20px; height: 20px; }

.toggle-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  background: white;
  border-radius: var(--radius-sm);
  margin-top: 16px;
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

.oobe-nav {
  display: flex;
  justify-content: center;
  gap: 12px;
  padding: 20px 32px;
  background: white;
  border-top: 1px solid var(--border);
}

.btn-primary, .btn-secondary, .btn-skip {
  padding: 8px 24px;
  border-radius: var(--radius-sm);
  font-size: 14px;
  font-weight: 500;
}

.btn-primary { background: var(--brand); color: white; }
.btn-primary:hover { background: var(--brand-hover); }

.btn-secondary { background: var(--bg-hover); color: var(--text); }
.btn-secondary:hover { background: var(--border); }

.nav-placeholder {
  visibility: hidden;
  pointer-events: none;
}

.btn-skip { background: transparent; color: var(--text-secondary); }
.btn-skip:hover { color: var(--text); }
</style>

<template>
  <div class="panel-content">
    <!-- 到期弹窗覆盖层 -->
    <div v-if="showDueOverlay" class="due-overlay" @click="dismissDue">
      <div class="due-card" @click.stop>
        <div class="due-bell">
          <img :src="icon('bell')" alt="提醒" />
        </div>
        <div class="due-title">提醒时间到</div>
        <div class="due-note">{{ dueItem?.note || '您有一条提醒' }}</div>
        <div class="due-actions">
          <button class="due-btn primary" @click="snooze(5)">5分钟后再次提醒</button>
          <button class="due-btn" @click="snooze(15)">15分钟后</button>
          <button class="due-btn text" @click="dismissDue">我知道了</button>
        </div>
      </div>
    </div>

    <!-- 添加表单 (内嵌，非弹窗) -->
    <div v-if="showForm" class="form-card">
      <div class="form-header">
        <span class="form-title">{{ editingId ? '编辑提醒' : '添加提醒' }}</span>
        <button class="form-close" @click="closeForm">×</button>
      </div>

      <div class="form-body">
        <div class="form-field">
          <label>提醒内容</label>
          <input v-model="formNote" type="text" placeholder="例如：下节课是数学课" maxlength="60" ref="noteInput" />
        </div>

        <div class="form-field">
          <label>提醒类型</label>
          <div class="kind-select">
            <button v-for="k in kindOptions" :key="k.value" :class="{ active: formKind === k.value }" @click="formKind = k.value">{{ k.label }}</button>
          </div>
        </div>

        <div v-if="formKind === 'once'" class="form-field">
          <label>提醒时间</label>
          <input v-model="formTime" type="time" />
        </div>

        <div v-if="formKind === 'interval'" class="form-field">
          <label>重复间隔</label>
          <div class="interval-row">
            <input v-model.number="formMinutes" type="number" min="1" max="1440" />
            <span>分钟</span>
          </div>
        </div>

        <div v-if="formKind === 'hourly'" class="form-field hint">将每个整点触发提醒</div>
      </div>

      <div class="form-actions">
        <button class="btn-save" :disabled="!canSave" @click="saveReminder">{{ editingId ? '保存' : '添加' }}</button>
      </div>
    </div>

    <!-- 铃声设置 -->
    <div v-if="!showForm" class="sound-card">
      <div class="sound-header" @click="showSoundSettings = !showSoundSettings">
        <span class="sound-title">铃声设置</span>
        <span class="sound-toggle">{{ showSoundSettings ? '▾' : '▸' }}</span>
      </div>
      <div v-show="showSoundSettings" class="sound-body">
        <div class="sound-field">
          <label>铃声类型</label>
          <div class="preset-select">
            <button v-for="p in soundPresets" :key="p.value" :class="{ active: soundConfig.preset === p.value }" @click="setPreset(p.value)">
              {{ p.label }}
            </button>
          </div>
        </div>

        <div class="sound-field">
          <label>音量 {{ Math.round(soundConfig.volume * 100) }}%</label>
          <input type="range" min="0" max="100" :value="Math.round(soundConfig.volume * 100)" @input="e => setVolume(Number((e.target as HTMLInputElement).value) / 100)" class="volume-slider" />
        </div>

        <div class="sound-field">
          <label>重复次数 {{ soundConfig.repeat }} 次</label>
          <input type="range" min="1" max="5" v-model.number="soundConfig.repeat" @change="saveSoundConfig" class="volume-slider" />
        </div>

        <div class="sound-field">
          <label>自定义铃声</label>
          <div class="mp3-row">
            <button class="btn-mp3" @click="selectMp3">{{ soundConfig.mp3Path ? '更换 MP3' : '选择 MP3 文件' }}</button>
            <button v-if="soundConfig.mp3Path" class="btn-mp3 clear" @click="clearMp3">清除</button>
          </div>
          <div v-if="soundConfig.mp3Path" class="mp3-path">{{ basename(soundConfig.mp3Path) }}</div>
        </div>

        <div class="sound-field">
          <button class="btn-test" @click="playTest">
            <span class="test-icon">▶</span> 试听铃声
          </button>
        </div>
      </div>
    </div>

    <!-- 快速添加 -->
    <div v-if="!showForm" class="quick-bar">
      <button v-for="q in quickOptions" :key="q.label" class="quick-chip" @click="quickAdd(q.minutes, q.label)">{{ q.label }}</button>
      <button class="quick-chip custom" @click="openForm()">自定义</button>
    </div>

    <!-- 提醒列表 -->
    <div class="reminder-list" v-if="sortedReminders.length > 0">
      <div class="reminder-item" v-for="r in sortedReminders" :key="r.id" :class="{ due: isDue(r), soon: isSoon(r) }">
        <img :src="icon('reminder')" class="reminder-icon" />
        <div class="reminder-info">
          <span class="reminder-note">{{ r.note || '提醒' }}</span>
          <span class="reminder-time">{{ formatTimeFull(r) }}</span>
        </div>
        <div class="reminder-actions">
          <button class="btn-icon" @click="editReminder(r)" title="编辑">✎</button>
          <button class="btn-icon" @click="remove(r.id)" title="删除">×</button>
        </div>
      </div>
    </div>

    <div v-else class="reminder-empty">
      <img :src="icon('bell')" class="empty-icon" />
      <p>暂无提醒</p>
      <p class="empty-hint">点击上方按钮快速添加</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import type { Reminder, ReminderSoundConfig } from '../../shared/types'

const reminders = ref<Reminder[]>([])
const showForm = ref(false)
const editingId = ref<string | null>(null)
const formNote = ref('')
const formKind = ref<'once' | 'interval' | 'hourly'>('once')
const formTime = ref('')
const formMinutes = ref(30)
const noteInput = ref<HTMLInputElement | null>(null)
const showDueOverlay = ref(false)
const dueItem = ref<Reminder | null>(null)
const now = ref(Date.now())

// 铃声设置
const showSoundSettings = ref(false)
const soundConfig = ref<ReminderSoundConfig>({
  preset: 'default',
  mp3Path: null,
  volume: 0.8,
  repeat: 3,
  repeatInterval: 800
})

const kindOptions = [
  { value: 'once' as const, label: '一次性' },
  { value: 'interval' as const, label: '周期性' },
  { value: 'hourly' as const, label: '每小时' }
]

const quickOptions = [
  { label: '5分钟后', minutes: 5 },
  { label: '10分钟后', minutes: 10 },
  { label: '30分钟后', minutes: 30 },
  { label: '1小时后', minutes: 60 }
]

const soundPresets = [
  { value: 'default', label: '默认' },
  { value: 'bell', label: '叮咚' },
  { value: 'alarm', label: '闹钟' },
  { value: 'custom', label: '自定义' }
]

let unsubDue: (() => void) | null = null
let timerId: ReturnType<typeof setInterval> | null = null

const canSave = computed(() => {
  if (!formNote.value.trim()) return false
  if (formKind.value === 'once' && !formTime.value) return false
  if (formKind.value === 'interval' && (!formMinutes.value || formMinutes.value < 1)) return false
  return true
})

const sortedReminders = computed(() => [...reminders.value].sort((a, b) => a.at - b.at))

function icon(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

function isDue(r: Reminder): boolean {
  if (r.snoozedUntil && now.value < r.snoozedUntil) return false
  return now.value >= r.at
}

function isSoon(r: Reminder): boolean {
  if (isDue(r)) return false
  const diff = r.at - now.value
  return diff > 0 && diff <= 300000
}

function formatAbsTime(at: number): string {
  const d = new Date(at)
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

function formatRelative(ms: number): string {
  if (ms <= 0) return '已到期'
  const m = Math.ceil(ms / 60000)
  if (m < 1) return '即将到期'
  if (m < 60) return `${m}分钟后`
  const h = Math.floor(m / 60)
  const rm = m % 60
  if (rm === 0) return `${h}小时后`
  return `${h}小时${rm}分钟后`
}

function formatTimeFull(r: Reminder): string {
  if (r.kind === 'interval' && r.repeatMin) return `每${r.repeatMin}分钟 下次${formatAbsTime(r.at)}`
  if (r.kind === 'hourly') return `每小时 下次${formatAbsTime(r.at)}`
  const d = new Date(r.at)
  const today = new Date()
  const isToday = d.toDateString() === today.toDateString()
  const abs = isToday ? formatAbsTime(r.at) : `${d.getMonth() + 1}/${d.getDate()} ${formatAbsTime(r.at)}`
  return `${abs} (${formatRelative(r.at - now.value)})`
}

function openForm(edit?: Reminder) {
  showForm.value = true
  if (edit) {
    editingId.value = edit.id
    formNote.value = edit.note || ''
    formKind.value = edit.kind
    if (edit.kind === 'once') {
      const d = new Date(edit.at)
      formTime.value = `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
    }
    if (edit.kind === 'interval') formMinutes.value = edit.repeatMin || 30
  } else {
    editingId.value = null
    formNote.value = ''
    formKind.value = 'once'
    const nd = new Date()
    nd.setMinutes(nd.getMinutes() + 5)
    formTime.value = `${String(nd.getHours()).padStart(2, '0')}:${String(nd.getMinutes()).padStart(2, '0')}`
    formMinutes.value = 30
  }
  nextTick(() => noteInput.value?.focus())
}

function closeForm() {
  showForm.value = false
  editingId.value = null
}

function editReminder(r: Reminder) { openForm(r) }

async function saveReminder() {
  if (!canSave.value) return
  const note = formNote.value.trim()
  let at = Date.now()
  let repeatMin: number | undefined
  if (formKind.value === 'once') {
    const [h, m] = formTime.value.split(':').map(Number)
    const target = new Date()
    target.setHours(h, m, 0, 0)
    if (target.getTime() <= Date.now()) target.setDate(target.getDate() + 1)
    at = target.getTime()
  } else if (formKind.value === 'interval') {
    repeatMin = formMinutes.value
    at = Date.now() + repeatMin * 60000
  } else if (formKind.value === 'hourly') {
    const nh = new Date()
    nh.setHours(nh.getHours() + 1, 0, 0, 0)
    at = nh.getTime()
  }
  if (editingId.value) {
    await window.sidekick.reminder.remove(editingId.value)
    reminders.value = reminders.value.filter(r => r.id !== editingId.value)
  }
  const r: Reminder = { id: editingId.value || `r${Date.now()}`, kind: formKind.value, at, note, repeatMin }
  await window.sidekick.reminder.add(r)
  reminders.value.push(r)
  closeForm()
}

async function quickAdd(minutes: number, label: string) {
  const r: Reminder = { id: `r${Date.now()}`, kind: 'once', at: Date.now() + minutes * 60000, note: `${label}的提醒` }
  await window.sidekick.reminder.add(r)
  reminders.value.push(r)
}

async function remove(id: string) {
  await window.sidekick.reminder.remove(id)
  reminders.value = reminders.value.filter(r => r.id !== id)
}

function dismissDue() { showDueOverlay.value = false; dueItem.value = null }

async function snooze(minutes: number) {
  if (!dueItem.value) return
  const r = dueItem.value
  r.snoozedUntil = Date.now() + minutes * 60000
  await window.sidekick.reminder.add(r)
  dismissDue()
}

async function loadReminders() {
  try { reminders.value = (await window.sidekick.reminder.list()) || [] } catch { /* noop */ }
}

async function loadSoundConfig() {
  try {
    const cfg = await window.sidekick.config.get()
    if (cfg.reminderSound) {
      soundConfig.value = { ...soundConfig.value, ...cfg.reminderSound }
    }
  } catch { /* noop */ }
}

function setPreset(preset: string) {
  soundConfig.value.preset = preset
  saveSoundConfig()
}

function setVolume(v: number) {
  soundConfig.value.volume = Math.max(0, Math.min(1, v))
  saveSoundConfig()
}

async function saveSoundConfig() {
  try {
    await window.sidekick.config.set('reminderSound', { ...soundConfig.value })
  } catch { /* noop */ }
}

async function selectMp3() {
  try {
    const path = await window.sidekick.reminder.selectSound()
    if (path) {
      soundConfig.value.mp3Path = path
      soundConfig.value.preset = 'custom'
      saveSoundConfig()
    }
  } catch { /* noop */ }
}

function clearMp3() {
  soundConfig.value.mp3Path = null
  soundConfig.value.preset = 'default'
  saveSoundConfig()
}

function basename(path: string): string {
  const parts = path.replace(/\\/g, '/').split('/')
  return parts[parts.length - 1] || path
}

async function playTest() {
  try {
    await window.sidekick.reminder.playTest({ ...soundConfig.value })
  } catch {
    // 降级: 本地 Web Audio 试听
    playLocalTest()
  }
}

/** 本地 Web Audio 试听 (3 声 louder beep) */
function playLocalTest() {
  try {
    const AudioCtx = window.AudioContext || (window as any).webkitAudioContext
    if (!AudioCtx) return
    const ctx = new AudioCtx()
    const vol = soundConfig.value.volume
    const count = soundConfig.value.repeat
    const interval = soundConfig.value.repeatInterval

    for (let i = 0; i < count; i++) {
      const t = ctx.currentTime + i * (interval / 1000)
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()
      osc.connect(gain)
      gain.connect(ctx.destination)

      // 不同预设使用不同频率序列
      const freqs = soundConfig.value.preset === 'alarm'
        ? [880, 660, 880]
        : soundConfig.value.preset === 'bell'
          ? [1047, 1319, 1568]
          : [880, 1100, 1320]
      osc.frequency.value = freqs[i % freqs.length] || 880

      gain.gain.setValueAtTime(vol * 0.8, t)
      gain.gain.exponentialRampToValueAtTime(0.01, t + 0.3)
      osc.start(t)
      osc.stop(t + 0.35)
    }

    setTimeout(() => ctx.close(), count * interval + 500)
  } catch { /* noop */ }
}

onMounted(() => {
  loadReminders()
  loadSoundConfig()
  timerId = setInterval(() => { now.value = Date.now() }, 30000)
  unsubDue = window.sidekick.reminder.onDue((r: Reminder) => {
    dueItem.value = r
    showDueOverlay.value = true
    // 本地增强提示音 ( louder + 3 声 )
    playLocalTest()
  })
})

onUnmounted(() => {
  if (timerId) clearInterval(timerId)
  if (unsubDue) unsubDue()
})
</script>

<style scoped>
.panel-content { display: flex; flex-direction: column; gap: 10px; }

.due-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.45); display: flex; align-items: center; justify-content: center; z-index: 100; animation: fadeIn 200ms ease; }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
.due-card { background: var(--bg); border-radius: var(--radius-lg); padding: 24px; width: 280px; text-align: center; box-shadow: 0 8px 32px rgba(0,0,0,0.2); animation: scaleIn 200ms ease; }
@keyframes scaleIn { from { transform: scale(0.9); opacity: 0; } to { transform: scale(1); opacity: 1; } }
.due-bell { width: 48px; height: 48px; margin: 0 auto 12px; background: var(--brand-light); border-radius: 50%; display: flex; align-items: center; justify-content: center; }
.due-bell img { width: 20px; height: 20px; }
.due-title { font-size: 15px; font-weight: 600; margin-bottom: 8px; }
.due-note { font-size: 13px; color: var(--text-secondary); margin-bottom: 16px; }
.due-actions { display: flex; flex-direction: column; gap: 8px; }
.due-btn { padding: 8px 12px; border-radius: var(--radius-sm); font-size: 13px; background: var(--bg-hover); color: var(--text); }
.due-btn.primary { background: var(--brand); color: white; }
.due-btn.text { background: transparent; color: var(--text-secondary); }
.due-btn:hover { opacity: 0.85; }

.form-card { background: var(--bg-hover); border-radius: var(--radius-md); padding: 12px; border: 1px solid var(--border); }
.form-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.form-title { font-size: 14px; font-weight: 600; }
.form-close { width: 20px; height: 20px; font-size: 16px; color: var(--text-secondary); background: transparent; border-radius: 50%; }
.form-close:hover { background: var(--border); }
.form-field { margin-bottom: 10px; }
.form-field label { display: block; font-size: 11px; color: var(--text-secondary); margin-bottom: 4px; }
.form-field input { width: 100%; padding: 6px 8px; border: 1px solid var(--border); border-radius: var(--radius-sm); font-size: 13px; background: var(--bg); color: var(--text); outline: none; box-sizing: border-box; }
.form-field input:focus { border-color: var(--brand); }
.form-field.hint { font-size: 12px; color: var(--text-secondary); padding: 4px 0; }
.kind-select { display: flex; gap: 6px; }
.kind-select button { flex: 1; padding: 6px; font-size: 12px; border-radius: var(--radius-sm); background: var(--bg); border: 1px solid var(--border); color: var(--text-secondary); }
.kind-select button.active { background: var(--brand-light); border-color: var(--brand); color: var(--brand); }
.interval-row { display: flex; align-items: center; gap: 6px; }
.interval-row input { width: 80px; }
.interval-row span { font-size: 12px; color: var(--text-secondary); }
.form-actions { display: flex; justify-content: flex-end; margin-top: 8px; }
.btn-save { padding: 6px 16px; background: var(--brand); color: white; border-radius: var(--radius-sm); font-size: 13px; }
.btn-save:disabled { background: var(--border); color: var(--text-disabled); cursor: not-allowed; }
.btn-save:hover:not(:disabled) { background: var(--brand-hover); }

/* 铃声设置 */
.sound-card { background: var(--bg-hover); border-radius: var(--radius-md); border: 1px solid var(--border); overflow: hidden; }
.sound-header { display: flex; justify-content: space-between; align-items: center; padding: 10px 12px; cursor: pointer; user-select: none; }
.sound-header:hover { background: rgba(0,0,0,0.03); }
.sound-title { font-size: 13px; font-weight: 600; }
.sound-toggle { font-size: 12px; color: var(--text-secondary); }
.sound-body { padding: 0 12px 12px; display: flex; flex-direction: column; gap: 10px; }
.sound-field label { display: block; font-size: 11px; color: var(--text-secondary); margin-bottom: 4px; }
.preset-select { display: flex; gap: 6px; flex-wrap: wrap; }
.preset-select button { flex: 1; min-width: 60px; padding: 5px 8px; font-size: 12px; border-radius: var(--radius-sm); background: var(--bg); border: 1px solid var(--border); color: var(--text-secondary); }
.preset-select button.active { background: var(--brand-light); border-color: var(--brand); color: var(--brand); }
.volume-slider { width: 100%; height: 6px; -webkit-appearance: none; appearance: none; background: var(--border); border-radius: 3px; outline: none; }
.volume-slider::-webkit-slider-thumb { -webkit-appearance: none; width: 14px; height: 14px; border-radius: 50%; background: var(--brand); cursor: pointer; }
.mp3-row { display: flex; gap: 6px; }
.btn-mp3 { padding: 5px 10px; font-size: 12px; border-radius: var(--radius-sm); background: var(--brand-light); border: 1px solid var(--brand); color: var(--brand); }
.btn-mp3.clear { background: transparent; border-color: var(--border); color: var(--text-secondary); }
.mp3-path { font-size: 11px; color: var(--text-secondary); margin-top: 4px; word-break: break-all; }
.btn-test { width: 100%; padding: 8px; font-size: 13px; border-radius: var(--radius-sm); background: var(--brand-light); border: 1px solid var(--brand); color: var(--brand); display: flex; align-items: center; justify-content: center; gap: 6px; }
.btn-test:hover { background: var(--brand); color: white; }
.test-icon { font-size: 12px; }

.quick-bar { display: flex; flex-wrap: wrap; gap: 6px; }
.quick-chip { padding: 6px 10px; border-radius: var(--radius-sm); font-size: 12px; background: var(--bg-hover); border: 1px solid var(--border); color: var(--text); }
.quick-chip.custom { background: var(--brand-light); border-color: var(--brand); color: var(--brand); }
.quick-chip:hover { border-color: var(--brand); color: var(--brand); }

.reminder-list { display: flex; flex-direction: column; gap: 6px; }
.reminder-item { display: flex; align-items: center; gap: 10px; padding: 8px 10px; background: var(--bg-hover); border-radius: var(--radius-sm); border: 1px solid transparent; transition: all var(--anim); }
.reminder-item.due { border-color: var(--danger); background: rgba(231,76,60,0.06); }
.reminder-item.soon { border-color: var(--warn); background: rgba(243,156,18,0.06); }
.reminder-icon { width: 18px; height: 18px; opacity: 0.7; }
.reminder-info { flex: 1; display: flex; flex-direction: column; min-width: 0; }
.reminder-note { font-size: 13px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.reminder-time { font-size: 11px; color: var(--text-secondary); }
.reminder-item.due .reminder-time { color: var(--danger); font-weight: 600; }
.reminder-item.soon .reminder-time { color: var(--warn); font-weight: 600; }
.reminder-actions { display: flex; gap: 4px; }
.btn-icon { width: 20px; height: 20px; font-size: 14px; color: var(--text-secondary); background: transparent; border-radius: 50%; display: flex; align-items: center; justify-content: center; }
.btn-icon:hover { background: var(--border); color: var(--text); }

.reminder-empty { text-align: center; padding: 24px 0; color: var(--text-secondary); }
.empty-icon { width: 32px; height: 32px; opacity: 0.4; margin-bottom: 8px; }
.empty-hint { font-size: 11px; margin-top: 4px; }
</style>

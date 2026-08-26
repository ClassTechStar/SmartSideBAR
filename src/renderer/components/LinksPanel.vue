<template>
  <div class="panel-content">
    <!-- 链接列表 -->
    <div class="link-list" v-if="links.length > 0">
      <div class="link-item" v-for="(link, index) in links" :key="link.id" :class="{ edit: editingId === link.id }">
        <!-- 链接内容 -->
        <div v-if="editingId !== link.id" class="link-body" @click="openLink(link.url)">
          <img :src="icon('link')" class="link-icon" />
          <span class="link-name">{{ link.name }}</span>
        </div>

        <!-- 编辑表单 -->
        <div v-else class="edit-form">
          <input ref="editInput" v-model="editName" placeholder="链接名称" class="form-input" />
          <input v-model="editUrl" placeholder="https://..." class="form-input" />
          <div class="edit-actions">
            <button class="btn-save" @click="saveEdit(index)">保存</button>
            <button class="btn-cancel" @click="cancelEdit">取消</button>
          </div>
        </div>

        <!-- 操作按钮 -->
        <div v-if="editingId !== link.id" class="link-actions">
          <button class="btn-action up" @click.stop="moveUp(index)" :disabled="index === 0" title="上移">▲</button>
          <button class="btn-action down" @click.stop="moveDown(index)" :disabled="index === links.length - 1" title="下移">▼</button>
          <button class="btn-action edit" @click.stop="startEdit(link)" title="编辑">✎</button>
          <button class="btn-action delete" @click.stop="removeLink(index)" title="删除">✕</button>
        </div>
      </div>
    </div>

    <!-- 空状态 -->
    <div v-else class="empty-state">
      <img :src="icon('link')" class="empty-icon" />
      <p>暂无快捷链接</p>
    </div>

    <!-- 添加新链接 -->
    <div class="add-section">
      <button v-if="!showAddForm" class="btn-add" @click="showAddForm = true">
        <span class="add-icon">+</span>
        <span>添加链接</span>
      </button>

      <div v-else class="add-form">
        <input ref="addInput" v-model="newName" placeholder="链接名称 (如: 希沃白板)" class="form-input" @keyup.enter="focusUrl" />
        <input ref="urlInput" v-model="newUrl" placeholder="https://www.seewo.com" class="form-input" @keyup.enter="addLink" />
        <div class="form-error" v-if="errorMsg">{{ errorMsg }}</div>
        <div class="add-actions">
          <button class="btn-save" @click="addLink">保存</button>
          <button class="btn-cancel" @click="cancelAdd">取消</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick, onMounted, onUnmounted, watch } from 'vue'
import type { LinkItem } from '../../shared/types'

const links = ref<LinkItem[]>([])
const showAddForm = ref(false)
const newName = ref('')
const newUrl = ref('')
const errorMsg = ref('')
const editingId = ref<string | null>(null)
const editName = ref('')
const editUrl = ref('')

// 添加/编辑表单打开时, 阻止侧边栏自动收起
watch([showAddForm, editingId], ([showForm, editId]) => {
  ;(window as any).__keepSidebarOpen = showForm || editId !== null
})

function icon(name: string): string {
  return new URL(`../assets/icons/${name}.svg`, import.meta.url).href
}

async function loadLinks() {
  try {
    const cfg = await window.sidekick.config.get()
    links.value = cfg.links.filter(l => l.enabled && l.url && l.url.trim() !== '')
  } catch (e) { console.error('[Links] Load failed:', e) }
}

async function openLink(url: string) {
  if (!url) return
  const validated = normalizeUrl(url)
  if (!validated) {
    showError('链接地址无效')
    return
  }
  try {
    await window.sidekick.shell.openExternal(validated)
  } catch (e) { console.error(e) }
}

// URL 规范化: 自动补全协议,过滤空白
function normalizeUrl(url: string): string | null {
  const trimmed = url.trim()
  if (!trimmed) return null
  // 如果用户只输入了域名(不含空格),补全 https://
  if (/^[a-zA-Z0-9][-a-zA-Z0-9]*(\.[a-zA-Z][-a-zA-Z0-9]*)+$/.test(trimmed)) {
    return `https://${trimmed}`
  }
  // 已有协议
  if (/^https?:\/\//i.test(trimmed)) return trimmed
  // 其他情况: 如果包含 . 且无空格,认为是无协议URL
  if (trimmed.includes('.') && !trimmed.includes(' ')) {
    return `https://${trimmed}`
  }
  // 无效
  return null
}

function isValidUrl(url: string): boolean {
  return !!normalizeUrl(url)
}

function showError(msg: string) {
  errorMsg.value = msg
  setTimeout(() => errorMsg.value = '', 3000)
}

// 添加链接
async function addLink() {
  const name = newName.value.trim()
  const rawUrl = newUrl.value.trim()

  if (!name) {
    showError('请输入链接名称')
    return
  }
  if (!rawUrl) {
    showError('请输入链接地址')
    return
  }
  const url = normalizeUrl(rawUrl)
  if (!url) {
    showError('链接格式不正确，请检查是否有空格或无效字符')
    return
  }

  // 检查重复
  if (links.value.some(l => l.url === url)) {
    showError('该链接已存在')
    return
  }

  const newLink: LinkItem = {
    id: `l${Date.now()}`,
    name,
    url,
    enabled: true
  }

  links.value.push(newLink)
  await saveAll()

  // 重置表单
  newName.value = ''
  newUrl.value = ''
  showAddForm.value = false
  errorMsg.value = ''
}

function cancelAdd() {
  showAddForm.value = false
  newName.value = ''
  newUrl.value = ''
  errorMsg.value = ''
}

function focusUrl() {
  nextTick(() => {
    const el = document.querySelector('.add-form input[placeholder^="https"]') as HTMLInputElement
    el?.focus()
  })
}

// 编辑
function startEdit(link: LinkItem) {
  editingId.value = link.id
  editName.value = link.name
  editUrl.value = link.url
  nextTick(() => {
    const input = document.querySelector('.edit-form input') as HTMLInputElement
    input?.focus()
  })
}

function cancelEdit() {
  editingId.value = null
  editName.value = ''
  editUrl.value = ''
}

async function saveEdit(index: number) {
  const name = editName.value.trim()
  const rawUrl = editUrl.value.trim()

  if (!name) {
    showError('名称不能为空')
    return
  }
  const url = normalizeUrl(rawUrl)
  if (!url) {
    showError('链接格式不正确')
    return
  }

  links.value[index].name = name
  links.value[index].url = url
  await saveAll()
  editingId.value = null
}

// 删除
async function removeLink(index: number) {
  const name = links.value[index].name
  // 直接删除,无需二次确认(教师操作要爽快,误删可重新添加)
  links.value.splice(index, 1)
  await saveAll()
  console.log(`[Links] Removed: ${name}`)
}

// 排序
function moveUp(index: number) {
  if (index <= 0) return
  const tmp = links.value[index]
  links.value[index] = links.value[index - 1]
  links.value[index - 1] = tmp
  saveAll()
}

function moveDown(index: number) {
  if (index >= links.value.length - 1) return
  const tmp = links.value[index]
  links.value[index] = links.value[index + 1]
  links.value[index + 1] = tmp
  saveAll()
}

// 保存到配置
async function saveAll() {
  try {
    // 写入完整 links 数组(包含 enabled=true)
    const allLinks = links.value.map(l => ({ ...l, enabled: true }))
    await window.sidekick.config.set('links', allLinks)
  } catch (e) {
    console.error('[Links] Save failed:', e)
    showError('保存失败,请重试')
  }
}

onMounted(loadLinks)

onUnmounted(() => {
  ;(window as any).__keepSidebarOpen = false
})
</script>

<style scoped>
.panel-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* 链接列表 */
.link-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.link-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  background: var(--bg-hover);
  border-radius: var(--radius-sm);
  transition: background 120ms ease;
}

.link-item.edit {
  flex-direction: column;
  align-items: stretch;
  gap: 8px;
}

.link-body {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  min-width: 0;
  overflow: hidden;
}

.link-body:hover {
  color: var(--brand);
}

.link-icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

.link-name {
  font-size: 14px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 操作按钮 */
.link-actions {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.btn-action {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  font-size: 12px;
  color: var(--text-disabled);
  background: transparent;
  border: none;
  cursor: pointer;
}

.btn-action:hover {
  background: rgba(0, 0, 0, 0.08);
  color: var(--text);
}

.btn-action:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.btn-action.delete:hover {
  background: rgba(231, 76, 60, 0.1);
  color: #e74c3c;
}

.btn-action.edit:hover {
  color: var(--brand);
}

/* 编辑表单 */
.edit-form {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.edit-actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}

/* 空状态 */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 32px 16px;
  color: var(--text-disabled);
}

.empty-icon {
  width: 32px;
  height: 32px;
  opacity: 0.4;
}

/* 添加区 */
.add-section {
  border-top: 1px solid var(--border);
  padding-top: 12px;
}

.btn-add {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 12px;
  border: 1.5px dashed var(--border);
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--text-secondary);
  font-size: 14px;
  cursor: pointer;
  transition: all 120ms ease;
}

.btn-add:hover {
  border-color: var(--brand);
  color: var(--brand);
  background: var(--brand-light);
}

.add-icon {
  font-size: 18px;
  font-weight: 300;
}

/* 表单 */
.add-form {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-input {
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  font-size: 14px;
  background: #fff;
  outline: none;
}

.form-input:focus {
  border-color: var(--brand);
  box-shadow: 0 0 0 2px rgba(43, 110, 224, 0.15);
}

.form-error {
  padding: 6px 10px;
  background: rgba(231, 76, 60, 0.08);
  border-radius: var(--radius-sm);
  font-size: 12px;
  color: #e74c3c;
}

.add-actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}

.btn-save {
  padding: 8px 16px;
  background: var(--brand);
  color: #fff;
  border-radius: var(--radius-sm);
  font-size: 13px;
  cursor: pointer;
}

.btn-save:hover {
  background: #1d5bc4;
}

.btn-cancel {
  padding: 8px 16px;
  background: transparent;
  color: var(--text-secondary);
  border-radius: var(--radius-sm);
  font-size: 13px;
  cursor: pointer;
}

.btn-cancel:hover {
  background: var(--bg-hover);
}
</style>

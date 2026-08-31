<template>
  <div class="settings-app">
    <div class="settings-header">
      <h1>设置</h1>
    </div>
    <div class="settings-body">
      <!-- P1-6: 统一设置表单 (与侧边栏共用同一实现, 字段不再漂移) -->
      <SettingsForm ref="formRef" @saved="onSaved" />
    </div>

    <div class="settings-footer">
      <button class="btn-save" @click="save">保存设置</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import SettingsForm from '../components/SettingsForm.vue'

const formRef = ref<InstanceType<typeof SettingsForm> | null>(null)

async function save() {
  try {
    await formRef.value?.save()
  } catch {
    alert('保存失败, 请查看日志')
  }
}

function onSaved() {
  alert('设置已保存')
}
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

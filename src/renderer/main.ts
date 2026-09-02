// src/renderer/main.ts - Vue 应用入口

import { createApp } from 'vue'
import { createRouter, createWebHashHistory } from 'vue-router'
import App from './App.vue'
import SidebarApp from './sidebar/SidebarApp.vue'
import OobeApp from './oobe/OobeApp.vue'
import OverlayApp from './overlay/OverlayApp.vue'
import AnnotateApp from './annotate/AnnotateApp.vue'
import RecorderApp from './recorder/RecorderApp.vue'
import SettingsApp from './settings/SettingsApp.vue'
import FloatBallApp from './floatball/FloatBallApp.vue'

// 液态玻璃设计系统 (v1.1 编译产物提取, 全局注入; 顺序 = 级联顺序)
import './styles/liquid-glass.css'
import './styles/sidebar-glass.css'
import './styles/floatball.css'
import './styles/glass-patch.css'
// 面板文字可读性覆盖 (白底蓝字, 固定不随主题变化; 必须最后导入)
import './styles/panel-contrast.css'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/sidebar', component: SidebarApp },
    { path: '/oobe', component: OobeApp },
    { path: '/overlay', component: OverlayApp },
    { path: '/annotate', component: AnnotateApp },
    { path: '/recorder', component: RecorderApp },
    { path: '/settings', component: SettingsApp },
    { path: '/floatball', component: FloatBallApp },
    { path: '/', redirect: '/sidebar' }
  ]
})

createApp(App).use(router).mount('#app')

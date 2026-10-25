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

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/sidebar', component: SidebarApp },
    { path: '/oobe', component: OobeApp },
    { path: '/overlay', component: OverlayApp },
    { path: '/annotate', component: AnnotateApp },
    { path: '/recorder', component: RecorderApp },
    { path: '/settings', component: SettingsApp },
    { path: '/', redirect: '/sidebar' }
  ]
})

createApp(App).use(router).mount('#app')

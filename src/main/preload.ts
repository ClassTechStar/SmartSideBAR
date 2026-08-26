// src/main/preload.ts - 预加载脚本 (contextBridge 白名单)

import { contextBridge, ipcRenderer } from 'electron'
import { IPC_CHANNELS } from '../shared/ipc-channels'

// 暴露给渲染进程的安全 API
const sidekickApi = {
  // IME
  ime: {
    getState: () => ipcRenderer.invoke(IPC_CHANNELS['ime:getState']),
    toggle: () => ipcRenderer.invoke(IPC_CHANNELS['ime:toggle']),
    onChanged: (cb: (state: any) => void) => {
      const handler = (_event: any, state: any) => cb(state)
      ipcRenderer.on(IPC_CHANNELS['ime:changed'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['ime:changed'], handler)
    }
  },

  // 截图
  capture: {
    region: (opts: any) => ipcRenderer.invoke(IPC_CHANNELS['capture:region'], opts),
    annotate: (opts: any) => ipcRenderer.invoke(IPC_CHANNELS['capture:annotate'], opts)
  },
  longshot: {
    start: (opts?: any) => ipcRenderer.invoke(IPC_CHANNELS['longshot:start'], opts),
    stop: () => ipcRenderer.invoke(IPC_CHANNELS['longshot:stop']),
    selectWindow: () => ipcRenderer.invoke(IPC_CHANNELS['longshot:selectWindow']),
    onProgress: (cb: (data: any) => void) => {
      const handler = (_event: any, data: any) => cb(data)
      ipcRenderer.on(IPC_CHANNELS['longshot:progress'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['longshot:progress'], handler)
    },
    onCountdown: (cb: (n: number) => void) => {
      const handler = (_event: any, n: number) => cb(n)
      ipcRenderer.on(IPC_CHANNELS['longshot:countdown'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['longshot:countdown'], handler)
    }
  },

  // 录屏
  recorder: {
    start: (opts?: any) => ipcRenderer.invoke(IPC_CHANNELS['recorder:start'], opts),
    stop: () => ipcRenderer.invoke(IPC_CHANNELS['recorder:stop']),
    status: () => ipcRenderer.invoke(IPC_CHANNELS['recorder:status']),
    onStatusChanged: (cb: (status: any) => void) => {
      const handler = (_event: any, status: any) => cb(status)
      ipcRenderer.on(IPC_CHANNELS['recorder:statusChanged'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['recorder:statusChanged'], handler)
    },
    // 以下供录屏页面内部使用
    sendData: (data: ArrayBuffer) => ipcRenderer.send(IPC_CHANNELS['recorder:data'], data),
    sendComplete: (data: ArrayBuffer, mimeType: string) =>
      ipcRenderer.send(IPC_CHANNELS['recorder:complete'], data, mimeType),
    sendStarted: () => ipcRenderer.send(IPC_CHANNELS['recorder:started']),
    onPageStart: (cb: (opts: any) => void) => {
      const handler = (_event: any, opts: any) => cb(opts)
      ipcRenderer.on(IPC_CHANNELS['recorder:pageStart'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['recorder:pageStart'], handler)
    },
    onPageStop: (cb: () => void) => {
      const handler = () => cb()
      ipcRenderer.on(IPC_CHANNELS['recorder:pageStop'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['recorder:pageStop'], handler)
    }
  },

  // 覆盖层 (区域截图 + 批注)
  overlay: {
    ready: () => ipcRenderer.send(IPC_CHANNELS['overlay:ready']),
    sendRegion: (region: any) => ipcRenderer.send(IPC_CHANNELS['overlay:region'], region),
    saveAnnotate: (dataUrl: string) => ipcRenderer.send(IPC_CHANNELS['overlay:saveAnnotate'], dataUrl),
    cancel: () => ipcRenderer.send(IPC_CHANNELS['overlay:cancel']),
    onInit: (cb: (init: any) => void) => {
      const handler = (_event: any, init: any) => cb(init)
      ipcRenderer.on(IPC_CHANNELS['overlay:init'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['overlay:init'], handler)
    }
  },

  // 显示
  display: {
    list: () => ipcRenderer.invoke(IPC_CHANNELS['display:list']),
    sidebarTarget: () => ipcRenderer.invoke(IPC_CHANNELS['display:sidebarTarget']),
    windowOrigin: () => Promise.resolve({ x: 0, y: 0 })
  },

  // 配置
  config: {
    get: () => ipcRenderer.invoke(IPC_CHANNELS['config:get']),
    set: (key: string, value: any) => ipcRenderer.invoke(IPC_CHANNELS['config:set'], key, value),
    onUpdated: (cb: (cfg: any) => void) => {
      const handler = (_event: any, cfg: any) => cb(cfg)
      ipcRenderer.on(IPC_CHANNELS['config:updated'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['config:updated'], handler)
    }
  },

  // OOBE
  oobe: {
    getState: () => ipcRenderer.invoke(IPC_CHANNELS['oobe:getState']),
    setState: (state: any) => ipcRenderer.invoke(IPC_CHANNELS['oobe:setState'], state),
    closeAndOpenMain: () => ipcRenderer.invoke(IPC_CHANNELS['oobe:closeAndOpenMain'])
  },

  // 电源
  power: {
    setAutoLaunch: (enabled: boolean) => ipcRenderer.invoke(IPC_CHANNELS['power:setAutoLaunch'], enabled)
  },

  // 系统操作
  shell: {
    openExternal: (url: string) => ipcRenderer.invoke(IPC_CHANNELS['shell:openExternal'], url),
    openPath: (path: string) => ipcRenderer.invoke(IPC_CHANNELS['shell:openPath'], path),
    showItemInFolder: (path: string) => ipcRenderer.invoke(IPC_CHANNELS['shell:showItemInFolder'], path)
  },

  // 任务管理器
  app: {
    openTaskMgr: () => ipcRenderer.invoke(IPC_CHANNELS['app:openTaskMgr'])
  },

  // 提醒
  reminder: {
    add: (r: any) => ipcRenderer.invoke(IPC_CHANNELS['reminder:add'], r),
    remove: (id: string) => ipcRenderer.invoke(IPC_CHANNELS['reminder:remove'], id),
    list: () => ipcRenderer.invoke(IPC_CHANNELS['reminder:list']),
    selectSound: () => ipcRenderer.invoke(IPC_CHANNELS['reminder:selectSound']),
    playTest: (cfg?: any) => ipcRenderer.invoke(IPC_CHANNELS['reminder:playTest'], cfg),
    onDue: (cb: (r: any) => void) => {
      const handler = (_event: any, r: any) => cb(r)
      ipcRenderer.on(IPC_CHANNELS['reminder:due'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['reminder:due'], handler)
    }
  },

  // 窗口
    window: {
      show: () => ipcRenderer.invoke(IPC_CHANNELS['window:show']),
      hide: () => ipcRenderer.invoke(IPC_CHANNELS['window:hide']),
      resize: (w: number, h: number) => ipcRenderer.invoke(IPC_CHANNELS['window:resize'], w, h),
      openSettings: () => ipcRenderer.invoke(IPC_CHANNELS['window:openSettings']),
      dock: () => ipcRenderer.invoke(IPC_CHANNELS['window:dock']),
      undock: () => ipcRenderer.invoke(IPC_CHANNELS['window:undock'])
    },

  // 通知
  notification: {
    show: (item: any) => ipcRenderer.invoke(IPC_CHANNELS['notification:show'], item),
    dismiss: (id: string) => ipcRenderer.invoke(IPC_CHANNELS['notification:dismiss'], id)
  },

  // USB
  usb: {
    list: () => ipcRenderer.invoke(IPC_CHANNELS['usb:list']),
    scan: () => ipcRenderer.invoke(IPC_CHANNELS['usb:scan']),
    getDiagnostics: () => ipcRenderer.invoke(IPC_CHANNELS['help:runDiagnostics']),
    onArrived: (cb: (drive: any) => void) => {
      const handler = (_event: any, drive: any) => cb(drive)
      ipcRenderer.on(IPC_CHANNELS['usb:arrived'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['usb:arrived'], handler)
    },
    onRemoved: (cb: (drive: any) => void) => {
      const handler = (_event: any, drive: any) => cb(drive)
      ipcRenderer.on(IPC_CHANNELS['usb:removed'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['usb:removed'], handler)
    }
  },

  // 诊断/调试
  diagnostic: {
    getStatus: () => ipcRenderer.invoke(IPC_CHANNELS['diag:getStatus']),
    runFull: () => ipcRenderer.invoke(IPC_CHANNELS['help:runDiagnostics']),
    exportPack: () => ipcRenderer.invoke(IPC_CHANNELS['help:exportDiagPack']),
    onUpdate: (cb: (data: any) => void) => {
      const handler = (_event: any, data: any) => cb(data)
      ipcRenderer.on(IPC_CHANNELS['diag:update'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['diag:update'], handler)
    }
  },

  // 打印机
  printer: {
    getStatus: () => ipcRenderer.invoke(IPC_CHANNELS['printer:status']),
    onChanged: (cb: (status: any) => void) => {
      const handler = (_event: any, status: any) => cb(status)
      ipcRenderer.on(IPC_CHANNELS['printer:changed'], handler)
      return () => ipcRenderer.off(IPC_CHANNELS['printer:changed'], handler)
    }
  }
}

// 冻结 API 防止篡改
Object.freeze(sidekickApi)
contextBridge.exposeInMainWorld('sidekick', sidekickApi)

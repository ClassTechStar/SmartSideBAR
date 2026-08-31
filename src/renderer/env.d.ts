// src/renderer/env.d.ts - 渲染进程环境类型声明

/// <reference types="vite/client" />

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<{}, {}, any>
  export default component
}

interface Window {
  sidekick: {
    ime: {
      getState: () => Promise<{ locale: string; isChinese: boolean; mode: 'cn' | 'en' }>
      toggle: () => Promise<{ locale: string; isChinese: boolean; mode: 'cn' | 'en' }>
      onChanged: (cb: (state: any) => void) => () => void
    }
    capture: {
      region: (opts: any) => Promise<any>
      annotate: (opts: any) => Promise<any>
    }
    longshot: {
      start: (opts?: any) => Promise<string | null>
      stop: () => Promise<void>
      selectWindow: () => Promise<any[]>
      onProgress: (cb: (data: any) => void) => () => void
      onCountdown: (cb: (n: number) => void) => () => void
    }
    recorder: {
      start: (opts?: any) => Promise<{ success: boolean; error?: string }>
      stop: () => Promise<{ success: boolean; filepath?: string; error?: string }>
      status: () => Promise<{ recording: boolean; elapsed: number }>
      onStatusChanged: (cb: (status: any) => void) => () => void
      sendData: (data: ArrayBuffer) => void
      sendComplete: (data: ArrayBuffer, mimeType: string) => void
      sendStarted: () => void
      ready: () => void
      onPageStart: (cb: (opts: any) => void) => () => void
      onPageStop: (cb: () => void) => () => void
    }
    overlay: {
      ready: () => void
      sendRegion: (region: any) => void
      saveAnnotate: (dataUrl: string) => void
      cancel: () => void
      onInit: (cb: (init: any) => void) => () => void
      onScreenshot: (cb: (dataUrl: string) => void) => () => void
    }
    display: {
      list: () => Promise<any[]>
      sidebarTarget: () => Promise<any>
    }
    config: {
      get: () => Promise<any>
      set: (key: string, value: any) => Promise<boolean>
      onUpdated: (cb: (cfg: any) => void) => () => void
    }
    oobe: {
      getState: () => Promise<any>
      setState: (state: any) => Promise<boolean>
      closeAndOpenMain: () => Promise<boolean>
    }
    power: {
      getAutoLaunch: () => Promise<boolean>
      setAutoLaunch: (enabled: boolean) => Promise<boolean>
    }
    shell: {
      openExternal: (url: string) => Promise<void>
      openPath: (path: string) => Promise<boolean>
      showItemInFolder: (path: string) => Promise<void>
    }
    app: {
      openTaskMgr: () => Promise<boolean>
    }
    reminder: {
      add: (r: any) => Promise<void>
      remove: (id: string) => Promise<void>
      list: () => Promise<any[]>
      selectSound: () => Promise<string | null>
      playTest: (cfg?: any) => Promise<void>
      onDue: (cb: (r: any) => void) => () => void
    }
    window: {
      show: () => Promise<void>
      hide: () => Promise<void>
      resize: (w: number, h: number) => Promise<void>
      openSettings: () => Promise<void>
      dock: () => Promise<void>
      undock: () => Promise<void>
    }
    notification: {
      show: (item: any) => Promise<void>
      dismiss: (id: string) => Promise<void>
    }
    usb: {
      list: () => Promise<any[]>
      scan: () => Promise<{ success: boolean; drives: any[] }>
      getDiagnostics: () => Promise<any>
      onArrived: (cb: (drive: any) => void) => () => void
      onRemoved: (cb: (drive: any) => void) => () => void
    }
    printer: {
      getStatus: () => Promise<any[]>
      onChanged: (cb: (status: any) => void) => () => void
    }
    // 诊断/调试
    diagnostic: {
      getStatus: () => Promise<any>
      runFull: () => Promise<any>
      exportPack: () => Promise<string>
      onUpdate: (cb: (data: any) => void) => () => void
    }
  }
}

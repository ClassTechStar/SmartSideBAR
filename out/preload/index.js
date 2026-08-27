"use strict";
const electron = require("electron");
const IPC_CHANNELS = {
  // IME 输入法
  "ime:getState": "ime:getState",
  "ime:toggle": "ime:toggle",
  "ime:locale": "ime:locale",
  "ime:changed": "ime:changed",
  // 截图
  "capture:region": "capture:region",
  "capture:annotate": "capture:annotate",
  "capture:longshot": "capture:longshot",
  // 长截图
  "longshot:start": "longshot:start",
  "longshot:stop": "longshot:stop",
  "longshot:progress": "longshot:progress",
  "longshot:selectWindow": "longshot:selectWindow",
  "longshot:countdown": "longshot:countdown",
  // 录屏
  "recorder:start": "recorder:start",
  "recorder:stop": "recorder:stop",
  "recorder:pause": "recorder:pause",
  "recorder:resume": "recorder:resume",
  "recorder:status": "recorder:status",
  "recorder:statusChanged": "recorder:statusChanged",
  "recorder:pageStart": "recorder:pageStart",
  "recorder:pageStop": "recorder:pageStop",
  "recorder:pagePause": "recorder:pagePause",
  "recorder:pageResume": "recorder:pageResume",
  "recorder:data": "recorder:data",
  "recorder:complete": "recorder:complete",
  "recorder:started": "recorder:started",
  // 覆盖层 (区域截图 + 批注)
  "overlay:init": "overlay:init",
  "overlay:region": "overlay:region",
  "overlay:saveAnnotate": "overlay:saveAnnotate",
  "overlay:cancel": "overlay:cancel",
  "overlay:ready": "overlay:ready",
  // USB
  "usb:arrived": "usb:arrived",
  "usb:removed": "usb:removed",
  "usb:list": "usb:list",
  "usb:scan": "usb:scan",
  // 打印机
  "printer:status": "printer:status",
  "printer:changed": "printer:changed",
  // 配置
  "config:get": "config:get",
  "config:set": "config:set",
  "config:updated": "config:updated",
  // 显示
  "display:list": "display:list",
  "display:metricsChanged": "display:metricsChanged",
  "display:sidebarTarget": "display:sidebarTarget",
  // OOBE
  "oobe:getState": "oobe:getState",
  "oobe:setState": "oobe:setState",
  "oobe:closeAndOpenMain": "oobe:closeAndOpenMain",
  // 电源/自启
  "power:setAutoLaunch": "power:setAutoLaunch",
  "power:getAutoLaunch": "power:getAutoLaunch",
  // 系统操作
  "shell:openExternal": "shell:openExternal",
  "shell:openPath": "shell:openPath",
  "shell:showItemInFolder": "shell:showItemInFolder",
  "app:openTaskMgr": "app:openTaskMgr",
  // 提醒
  "reminder:add": "reminder:add",
  "reminder:remove": "reminder:remove",
  "reminder:list": "reminder:list",
  "reminder:due": "reminder:due",
  "reminder:selectSound": "reminder:selectSound",
  "reminder:playTest": "reminder:playTest",
  // 窗口
  "window:show": "window:show",
  "window:hide": "window:hide",
  "window:minimize": "window:minimize",
  "window:resize": "window:resize",
  "window:openSettings": "window:openSettings",
  "window:dock": "window:dock",
  "window:undock": "window:undock",
  // 外观 / 液态玻璃
  "appearance:get": "appearance:get",
  "appearance:set": "appearance:set",
  "appearance:changed": "appearance:changed",
  // 悬浮球
  "floatball:show": "floatball:show",
  "floatball:hide": "floatball:hide",
  "floatball:toggle": "floatball:toggle",
  "floatball:dragStart": "floatball:dragStart",
  "floatball:dragEnd": "floatball:dragEnd",
  "floatball:expand": "floatball:expand",
  "floatball:collapse": "floatball:collapse",
  "floatball:action": "floatball:action",
  "floatball:layout": "floatball:layout",
  "floatball:setClickThrough": "floatball:setClickThrough",
  // 通知
  "notification:show": "notification:show",
  "notification:dismiss": "notification:dismiss",
  // 帮助/自检
  "help:runDiagnostics": "help:runDiagnostics",
  "help:exportDiagPack": "help:exportDiagPack",
  // 诊断
  "diag:getStatus": "diag:getStatus",
  "diag:update": "diag:update"
};
new Set(Object.keys(IPC_CHANNELS));
const sidekickApi = {
  // IME
  ime: {
    getState: () => electron.ipcRenderer.invoke(IPC_CHANNELS["ime:getState"]),
    toggle: () => electron.ipcRenderer.invoke(IPC_CHANNELS["ime:toggle"]),
    onChanged: (cb) => {
      const handler = (_event, state) => cb(state);
      electron.ipcRenderer.on(IPC_CHANNELS["ime:changed"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["ime:changed"], handler);
    }
  },
  // 截图
  capture: {
    region: (opts) => electron.ipcRenderer.invoke(IPC_CHANNELS["capture:region"], opts),
    annotate: (opts) => electron.ipcRenderer.invoke(IPC_CHANNELS["capture:annotate"], opts)
  },
  longshot: {
    start: (opts) => electron.ipcRenderer.invoke(IPC_CHANNELS["longshot:start"], opts),
    stop: () => electron.ipcRenderer.invoke(IPC_CHANNELS["longshot:stop"]),
    selectWindow: () => electron.ipcRenderer.invoke(IPC_CHANNELS["longshot:selectWindow"]),
    onProgress: (cb) => {
      const handler = (_event, data) => cb(data);
      electron.ipcRenderer.on(IPC_CHANNELS["longshot:progress"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["longshot:progress"], handler);
    },
    onCountdown: (cb) => {
      const handler = (_event, n) => cb(n);
      electron.ipcRenderer.on(IPC_CHANNELS["longshot:countdown"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["longshot:countdown"], handler);
    }
  },
  // 录屏
  recorder: {
    start: (opts) => electron.ipcRenderer.invoke(IPC_CHANNELS["recorder:start"], opts),
    stop: () => electron.ipcRenderer.invoke(IPC_CHANNELS["recorder:stop"]),
    pause: () => electron.ipcRenderer.invoke(IPC_CHANNELS["recorder:pause"]),
    resume: () => electron.ipcRenderer.invoke(IPC_CHANNELS["recorder:resume"]),
    status: () => electron.ipcRenderer.invoke(IPC_CHANNELS["recorder:status"]),
    onStatusChanged: (cb) => {
      const handler = (_event, status) => cb(status);
      electron.ipcRenderer.on(IPC_CHANNELS["recorder:statusChanged"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["recorder:statusChanged"], handler);
    },
    // 以下供录屏页面内部使用
    sendData: (data) => electron.ipcRenderer.send(IPC_CHANNELS["recorder:data"], data),
    sendComplete: (data, mimeType) => electron.ipcRenderer.send(IPC_CHANNELS["recorder:complete"], data, mimeType),
    sendStarted: () => electron.ipcRenderer.send(IPC_CHANNELS["recorder:started"]),
    onPageStart: (cb) => {
      const handler = (_event, opts) => cb(opts);
      electron.ipcRenderer.on(IPC_CHANNELS["recorder:pageStart"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["recorder:pageStart"], handler);
    },
    onPageStop: (cb) => {
      const handler = () => cb();
      electron.ipcRenderer.on(IPC_CHANNELS["recorder:pageStop"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["recorder:pageStop"], handler);
    },
    onPagePause: (cb) => {
      const handler = () => cb();
      electron.ipcRenderer.on(IPC_CHANNELS["recorder:pagePause"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["recorder:pagePause"], handler);
    },
    onPageResume: (cb) => {
      const handler = () => cb();
      electron.ipcRenderer.on(IPC_CHANNELS["recorder:pageResume"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["recorder:pageResume"], handler);
    }
  },
  // 覆盖层 (区域截图 + 批注)
  overlay: {
    ready: () => electron.ipcRenderer.send(IPC_CHANNELS["overlay:ready"]),
    sendRegion: (region) => electron.ipcRenderer.send(IPC_CHANNELS["overlay:region"], region),
    saveAnnotate: (dataUrl) => electron.ipcRenderer.send(IPC_CHANNELS["overlay:saveAnnotate"], dataUrl),
    cancel: () => electron.ipcRenderer.send(IPC_CHANNELS["overlay:cancel"]),
    onInit: (cb) => {
      const handler = (_event, init) => cb(init);
      electron.ipcRenderer.on(IPC_CHANNELS["overlay:init"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["overlay:init"], handler);
    }
  },
  // 显示
  display: {
    list: () => electron.ipcRenderer.invoke(IPC_CHANNELS["display:list"]),
    sidebarTarget: () => electron.ipcRenderer.invoke(IPC_CHANNELS["display:sidebarTarget"]),
    windowOrigin: () => Promise.resolve({ x: 0, y: 0 })
  },
  // 配置
  config: {
    get: () => electron.ipcRenderer.invoke(IPC_CHANNELS["config:get"]),
    set: (key, value) => electron.ipcRenderer.invoke(IPC_CHANNELS["config:set"], key, value),
    onUpdated: (cb) => {
      const handler = (_event, cfg) => cb(cfg);
      electron.ipcRenderer.on(IPC_CHANNELS["config:updated"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["config:updated"], handler);
    }
  },
  // OOBE
  oobe: {
    getState: () => electron.ipcRenderer.invoke(IPC_CHANNELS["oobe:getState"]),
    setState: (state) => electron.ipcRenderer.invoke(IPC_CHANNELS["oobe:setState"], state),
    closeAndOpenMain: () => electron.ipcRenderer.invoke(IPC_CHANNELS["oobe:closeAndOpenMain"])
  },
  // 电源
  power: {
    setAutoLaunch: (enabled) => electron.ipcRenderer.invoke(IPC_CHANNELS["power:setAutoLaunch"], enabled)
  },
  // 系统操作
  shell: {
    openExternal: (url) => electron.ipcRenderer.invoke(IPC_CHANNELS["shell:openExternal"], url),
    openPath: (path) => electron.ipcRenderer.invoke(IPC_CHANNELS["shell:openPath"], path),
    showItemInFolder: (path) => electron.ipcRenderer.invoke(IPC_CHANNELS["shell:showItemInFolder"], path)
  },
  // 任务管理器
  app: {
    openTaskMgr: () => electron.ipcRenderer.invoke(IPC_CHANNELS["app:openTaskMgr"])
  },
  // 提醒
  reminder: {
    add: (r) => electron.ipcRenderer.invoke(IPC_CHANNELS["reminder:add"], r),
    remove: (id) => electron.ipcRenderer.invoke(IPC_CHANNELS["reminder:remove"], id),
    list: () => electron.ipcRenderer.invoke(IPC_CHANNELS["reminder:list"]),
    selectSound: () => electron.ipcRenderer.invoke(IPC_CHANNELS["reminder:selectSound"]),
    playTest: (cfg) => electron.ipcRenderer.invoke(IPC_CHANNELS["reminder:playTest"], cfg),
    onDue: (cb) => {
      const handler = (_event, r) => cb(r);
      electron.ipcRenderer.on(IPC_CHANNELS["reminder:due"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["reminder:due"], handler);
    }
  },
  // 窗口
  window: {
    show: () => electron.ipcRenderer.invoke(IPC_CHANNELS["window:show"]),
    hide: () => electron.ipcRenderer.invoke(IPC_CHANNELS["window:hide"]),
    resize: (w, h) => electron.ipcRenderer.invoke(IPC_CHANNELS["window:resize"], w, h),
    openSettings: () => electron.ipcRenderer.invoke(IPC_CHANNELS["window:openSettings"]),
    dock: () => electron.ipcRenderer.invoke(IPC_CHANNELS["window:dock"]),
    undock: () => electron.ipcRenderer.invoke(IPC_CHANNELS["window:undock"])
  },
  // 外观 / 液态玻璃
  appearance: {
    get: () => electron.ipcRenderer.invoke(IPC_CHANNELS["appearance:get"]),
    set: (patch) => electron.ipcRenderer.invoke(IPC_CHANNELS["appearance:set"], patch),
    onChanged: (cb) => {
      const handler = (_event, snap) => cb(snap);
      electron.ipcRenderer.on(IPC_CHANNELS["appearance:changed"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["appearance:changed"], handler);
    }
  },
  // 悬浮球
  floatball: {
    show: () => electron.ipcRenderer.invoke(IPC_CHANNELS["floatball:show"]),
    hide: () => electron.ipcRenderer.invoke(IPC_CHANNELS["floatball:hide"]),
    toggle: () => electron.ipcRenderer.invoke(IPC_CHANNELS["floatball:toggle"]),
    dragStart: (grab) => electron.ipcRenderer.invoke(IPC_CHANNELS["floatball:dragStart"], grab),
    dragEnd: () => electron.ipcRenderer.invoke(IPC_CHANNELS["floatball:dragEnd"]),
    expand: () => electron.ipcRenderer.invoke(IPC_CHANNELS["floatball:expand"]),
    collapse: () => electron.ipcRenderer.invoke(IPC_CHANNELS["floatball:collapse"]),
    action: (id) => electron.ipcRenderer.invoke(IPC_CHANNELS["floatball:action"], id),
    setClickThrough: (on) => electron.ipcRenderer.send(IPC_CHANNELS["floatball:setClickThrough"], on),
    onLayout: (cb) => {
      const handler = (_event, layout) => cb(layout);
      electron.ipcRenderer.on(IPC_CHANNELS["floatball:layout"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["floatball:layout"], handler);
    }
  },
  // 通知
  notification: {
    show: (item) => electron.ipcRenderer.invoke(IPC_CHANNELS["notification:show"], item),
    dismiss: (id) => electron.ipcRenderer.invoke(IPC_CHANNELS["notification:dismiss"], id)
  },
  // USB
  usb: {
    list: () => electron.ipcRenderer.invoke(IPC_CHANNELS["usb:list"]),
    scan: () => electron.ipcRenderer.invoke(IPC_CHANNELS["usb:scan"]),
    getDiagnostics: () => electron.ipcRenderer.invoke(IPC_CHANNELS["help:runDiagnostics"]),
    onArrived: (cb) => {
      const handler = (_event, drive) => cb(drive);
      electron.ipcRenderer.on(IPC_CHANNELS["usb:arrived"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["usb:arrived"], handler);
    },
    onRemoved: (cb) => {
      const handler = (_event, drive) => cb(drive);
      electron.ipcRenderer.on(IPC_CHANNELS["usb:removed"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["usb:removed"], handler);
    }
  },
  // 诊断/调试
  diagnostic: {
    getStatus: () => electron.ipcRenderer.invoke(IPC_CHANNELS["diag:getStatus"]),
    runFull: () => electron.ipcRenderer.invoke(IPC_CHANNELS["help:runDiagnostics"]),
    exportPack: () => electron.ipcRenderer.invoke(IPC_CHANNELS["help:exportDiagPack"]),
    onUpdate: (cb) => {
      const handler = (_event, data) => cb(data);
      electron.ipcRenderer.on(IPC_CHANNELS["diag:update"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["diag:update"], handler);
    }
  },
  // 打印机
  printer: {
    getStatus: () => electron.ipcRenderer.invoke(IPC_CHANNELS["printer:status"]),
    onChanged: (cb) => {
      const handler = (_event, status) => cb(status);
      electron.ipcRenderer.on(IPC_CHANNELS["printer:changed"], handler);
      return () => electron.ipcRenderer.off(IPC_CHANNELS["printer:changed"], handler);
    }
  }
};
Object.freeze(sidekickApi);
electron.contextBridge.exposeInMainWorld("sidekick", sidekickApi);

// shared/ipc-channels.ts - IPC 通道白名单常量
// 所有 IPC 通道必须在白名单内注册,否则视为越权

export const IPC_CHANNELS = {
  // IME 输入法
  'ime:getState': 'ime:getState',
  'ime:toggle': 'ime:toggle',
  'ime:locale': 'ime:locale',
  'ime:changed': 'ime:changed',

  // 截图
  'capture:region': 'capture:region',
  'capture:annotate': 'capture:annotate',
  'capture:longshot': 'capture:longshot',

  // 长截图
  'longshot:start': 'longshot:start',
  'longshot:stop': 'longshot:stop',
  'longshot:progress': 'longshot:progress',
  'longshot:selectWindow': 'longshot:selectWindow',
  'longshot:countdown': 'longshot:countdown',

  // 录屏
  'recorder:start': 'recorder:start',
  'recorder:stop': 'recorder:stop',
  'recorder:status': 'recorder:status',
  'recorder:statusChanged': 'recorder:statusChanged',
  'recorder:pageStart': 'recorder:pageStart',
  'recorder:pageStop': 'recorder:pageStop',
  'recorder:data': 'recorder:data',
  'recorder:complete': 'recorder:complete',
  'recorder:started': 'recorder:started',

  // 覆盖层 (区域截图 + 批注)
  'overlay:init': 'overlay:init',
  'overlay:region': 'overlay:region',
  'overlay:saveAnnotate': 'overlay:saveAnnotate',
  'overlay:cancel': 'overlay:cancel',
  'overlay:ready': 'overlay:ready',

  // USB
  'usb:arrived': 'usb:arrived',
  'usb:removed': 'usb:removed',
  'usb:list': 'usb:list',
  'usb:scan': 'usb:scan',

  // 打印机
  'printer:status': 'printer:status',
  'printer:changed': 'printer:changed',

  // 配置
  'config:get': 'config:get',
  'config:set': 'config:set',
  'config:updated': 'config:updated',

  // 显示
  'display:list': 'display:list',
  'display:metricsChanged': 'display:metricsChanged',
  'display:sidebarTarget': 'display:sidebarTarget',

  // OOBE
  'oobe:getState': 'oobe:getState',
  'oobe:setState': 'oobe:setState',
  'oobe:closeAndOpenMain': 'oobe:closeAndOpenMain',

  // 电源/自启
  'power:setAutoLaunch': 'power:setAutoLaunch',
  'power:getAutoLaunch': 'power:getAutoLaunch',

  // 系统操作
  'shell:openExternal': 'shell:openExternal',
  'shell:openPath': 'shell:openPath',
  'shell:showItemInFolder': 'shell:showItemInFolder',
  'app:openTaskMgr': 'app:openTaskMgr',

  // 提醒
  'reminder:add': 'reminder:add',
  'reminder:remove': 'reminder:remove',
  'reminder:list': 'reminder:list',
  'reminder:due': 'reminder:due',
  'reminder:selectSound': 'reminder:selectSound',
  'reminder:playTest': 'reminder:playTest',

  // 窗口
  'window:show': 'window:show',
  'window:hide': 'window:hide',
  'window:minimize': 'window:minimize',
  'window:resize': 'window:resize',
  'window:openSettings': 'window:openSettings',
  'window:dock': 'window:dock',
  'window:undock': 'window:undock',

  // 通知
  'notification:show': 'notification:show',
  'notification:dismiss': 'notification:dismiss',

  // 帮助/自检
  'help:runDiagnostics': 'help:runDiagnostics',
  'help:exportDiagPack': 'help:exportDiagPack',

  // 诊断
  'diag:getStatus': 'diag:getStatus',
  'diag:update': 'diag:update',
} as const

export type IpcChannel = keyof typeof IPC_CHANNELS

// 白名单校验: 只允许 invoke/handle/send 的通道
export const ALLOWED_CHANNELS = new Set(Object.keys(IPC_CHANNELS))

export function isAllowedChannel(channel: string): boolean {
  return ALLOWED_CHANNELS.has(channel)
}

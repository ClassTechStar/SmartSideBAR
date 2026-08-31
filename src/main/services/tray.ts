// services/tray.ts - 系统托盘 (F10: 退出入口 + 通知归属)

import { app, Tray, Menu, nativeImage } from 'electron'
import { join } from 'path'
import log from 'electron-log'
import { WindowManager } from '../windows/manager'

let tray: Tray | null = null

function resolveIcon(): string {
  // dev: out/main/../../build/icon.ico -> 项目根 build/icon.ico
  // prod: asar 内 build/icon.ico
  return join(__dirname, '../../build/icon.ico')
}

export const TrayService = {
  init(): void {
    const iconPath = resolveIcon()
    let img = nativeImage.createFromPath(iconPath)

    // 兜底: 图标文件缺失时用 1x1 透明像素, 避免 Tray 构造抛异常
    if (img.isEmpty()) {
      log.warn('[Tray] Icon not found at', iconPath, ', using fallback')
      img = nativeImage.createEmpty()
    }

    // 缩小到 16x16 适配托盘尺寸
    const small = img.resize({ width: 16, height: 16 })

    tray = new Tray(small)
    tray.setToolTip('希沃侧边快捷键工具')

    const contextMenu = Menu.buildFromTemplate([
      {
        label: '显示侧边栏',
        click: () => WindowManager.showMain()
      },
      {
        label: '打开设置',
        click: () => WindowManager.showSettings()
      },
      { type: 'separator' },
      {
        label: '退出',
        click: () => {
          log.info('[Tray] User requested quit')
          app.quit()
        }
      }
    ])

    tray.setContextMenu(contextMenu)

    // 单击托盘图标: 显示侧边栏
    tray.on('click', () => {
      WindowManager.showMain()
    })

    log.info('[Tray] System tray initialized')
  },

  destroy(): void {
    if (tray) {
      tray.destroy()
      tray = null
    }
  }
}

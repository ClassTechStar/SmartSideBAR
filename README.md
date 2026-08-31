<div align="center">
  <img src="build/icon.ico" width="96" height="96" alt="SmartSideBAR Logo">
  <h1>SmartSideBAR</h1>
  <p><strong>常驻教学大屏侧边的快捷操作与设备提醒中心</strong></p>
  <p>
    <a href="#features"><img src="https://img.shields.io/badge/功能-9大模块-blue" alt="Features"></a>
    <a href="#tech-stack"><img src="https://img.shields.io/badge/技术栈-Electron%20+%20Vue%203-green" alt="Tech Stack"></a>
    <a href="#license"><img src="https://img.shields.io/badge/License-Apache%202.0-blue" alt="License"></a>
  </p>
</div>

---

## 项目介绍

**SmartSideBAR** 是一款专为希沃（Seewo）交互智能平板 / Windows OPS 教学环境设计的常驻侧边快捷键工具。它常驻屏幕侧边，一触即达，将输入法切换、区域截图与批注、长截图、录屏、U 盘监控、打印机状态、任务管理器、定时提醒、自定义链接等高频操作整合到一个低打扰的侧边抽屉中。

针对教师触屏场景优化，无需键盘快捷键，单手即可操作。内置完整的首次体验引导（OOBE），让非技术教师在 3 分钟内完成上手。

## 适用场景

- **希沃交互智能平板** / Windows OPS 教学一体机
- 4GB–8GB 内存的教育版 Windows 10/11 设备
- 触屏优先、低资源占用、低打扰的课堂辅助工具

## 功能列表

| 功能 | 说明 | 优先级 |
|------|------|--------|
| 输入法切换 | 一键中英文输入法切换（支持无边框/置顶焦点窗） | P0 |
| 区域截图 | 框选区域 → 批注 / 保存 / 复制，2 步内完成 | P0 |
| 屏幕批注 | 透明覆盖层直接在屏幕上圈画、书写，秒开秒画 | P0 |
| 长截图 | 选择窗口后自动滚动画布拼接长图 | P2 |
| 录屏 | WebM 转 MP4，默认 720p/15fps，轻量化 | P2 |
| U 盘监控 | 插入/移除 ≤2s 内可见提醒，支持多盘符合并 | P0 |
| 打印机状态 | 缺纸/卡纸/离线/墨量低高亮提醒，附故障速查 | P1 |
| 任务管理器 | 一键拉起系统任务管理器 | P1 |
| 自定义链接 | 3 个预置 + 6 个自定义槽位，名称/图标/顺序可调 | P1 |
| 快捷键槽位 | 3 个可配置全局热键，冲突自动检测并建议替代 | P1 |
| 定时提醒 | 整点/间隔/一次性三类，支持顺延/取消 | P1 |
| 首次体验引导 (OOBE) | 6 步向导，按角色裁剪，可跳过，≤3 分钟 | P0 |
| 白名单策略 | 支持 ProgramData 层策略下发，管控模块可见性 | P2 |

## 安装方法

### 方法一：NSIS 安装包（推荐）

```bash
# 1. 克隆仓库
git clone https://github.com/ClassTechStar/SmartSideBAR.git
cd SmartSideBAR

# 2. 安装依赖
npm install

# 3. 一键验证构建（类型检查 + 测试 + 打包 + 产出安装包）
npm run verify-build
```

构建完成后，在 `dist/` 目录下找到 `SmartSideBAR-安装包-{version}.exe`，双击即可安装。

### 方法二：开发模式运行

```bash
npm install
npm run dev
```

## 使用说明

### 日常使用

1. **展开侧边栏**：点击屏幕右侧（或左侧）的窄条图标轨
2. **收起侧边栏**：点击轨底部的 ↓ 箭头，或点击展开面板外的区域
3. **各功能面板**：点击图标轨上的图标，右侧展开对应功能面板
4. **全局截图热键**：默认 `Ctrl+Shift+A`（可在设置中修改）

### 首次使用（OOBE）

安装后首次启动会自动弹出 OOBE 引导窗口：

1. **欢迎** → 2. **选择角色**（数学/英语/化学/电教管理员）→ 3. **环境检测**（屏幕/触控/打印机/输入法）→ 4. **偏好设置**（勾选常用功能）→ 5. **上手练习**（展开/收起侧边栏）→ 6. **完成**

全程 ≤ 3 分钟，任一步可随时跳过。

### 设置

点击图标轨底部的齿轮图标进入设置页，可配置：
- 侧边栏位置（左/右）
- 截图保存目录与格式
- 录屏参数（帧率/码率/麦克风）
- 自定义链接槽位
- 提醒铃声（支持自定义 MP3）
- 开机自启

## 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| 框架 | [Electron](https://www.electronjs.org/) v30 | 桌面应用主框架 |
| 渲染层 | [Vue 3](https://vuejs.org/) + Composition API | UI 组件与响应式状态 |
| 构建工具 | [electron-vite](https://electron-vite.org/) | 主进程/Preload/渲染层统一构建 |
| 打包 | [electron-builder](https://www.electron.build/) | NSIS 单文件安装包 |
| 类型安全 | TypeScript 5.4 | 全栈类型覆盖 |
| 图像处理 | sharp | 截图裁剪与格式转换 |
| 日志 | electron-log | 文件日志轮转 |
| 更新 | electron-updater | 自动更新检测 |
| 测试 | Vitest | 单元测试 |

## 开发命令

```bash
# 开发模式（热重载）
npm run dev

# 类型检查
npm run typecheck

# 构建（类型检查 + 打包）
npm run build

# 运行测试
npm run test

# 持续测试模式
npm run test:watch

# 代码检查
npm run lint

# 构建 Windows 安装包
npm run win

# 完整验证构建（类型检查 + 测试 + 构建 + 打包）
npm run verify-build

# 预览生产构建
npm run preview
```

## 项目结构

```
SmartSideBAR/
├── build/                    # 构建资源（图标、NSIS 脚本）
│   ├── icon.ico
│   └── installer.nsh
├── resources/                # 额外资源文件
│   └── app/defaults/
│       └── config.json       # 默认配置模板
├── src/
│   ├── main/                 # 主进程（Node.js）
│   │   ├── main.ts           # 主进程入口
│   │   ├── preload.ts        # 上下文隔离 Preload 脚本
│   │   ├── services/         # 服务层
│   │   │   ├── config.ts     # 配置服务（三层覆盖：默认→ProgramData→AppData）
│   │   │   ├── display.ts    # 显示/DPI 适配服务
│   │   │   ├── ime.ts        # 输入法服务
│   │   │   ├── capture.ts    # 截图服务
│   │   │   ├── longshot.ts   # 长截图服务
│   │   │   ├── recorder.ts   # 录屏服务
│   │   │   ├── usb.ts        # U 盘监控服务
│   │   │   ├── printer.ts    # 打印机状态服务
│   │   │   ├── scheduler.ts  # 定时提醒服务
│   │   │   └── diagnostic.ts # 诊断服务
│   │   └── windows/
│   │       └── manager.ts    # 窗口管理器（侧边栏/OOBE/覆盖层/设置）
│   ├── renderer/             # 渲染进程（Vue 3）
│   │   ├── main.ts           # 渲染层入口
│   │   ├── App.vue
│   │   ├── index.html
│   │   ├── sidebar/          # 侧边栏主应用
│   │   ├── components/       # 功能面板组件
│   │   ├── oobe/             # OOBE 引导应用
│   │   ├── overlay/          # 区域截图覆盖层
│   │   ├── annotate/         # 批注覆盖层
│   │   ├── recorder/         # 录屏控制界面
│   │   ├── settings/         # 设置界面
│   │   └── assets/           # 图标资源
│   └── shared/               # 共享代码
│       ├── ipc-channels.ts   # IPC 通道白名单常量
│       ├── types.ts          # 核心数据模型与类型定义
│       └── vue-shims.d.ts
├── electron.vite.config.ts   # electron-vite 构建配置
├── electron-builder.yml      # electron-builder 打包配置
├── tsconfig.json             # TypeScript 配置
├── package.json
└── README.md
```

## 核心技术特性

### DPI 与多显示器适配

- 以 DIP（设备无关像素）为唯一业务坐标域
- 支持 100%–250% 系统缩放（含 1.75/2.25/2.5/3.0 非整数倍）
- 多显示器热插拔自动重定位（≤200ms）
- 4K@100% 与 1080p@100% 视觉尺寸一致

### 安全设计

- **IPC 白名单**：所有 IPC 通道必须在 `src/shared/ipc-channels.ts` 注册，未注册通道自动拦截
- **上下文隔离**：`contextIsolation: true`，主进程与渲染进程严格隔离
- **Preload 脚本**：唯一桥接层，仅暴露白名单 API

### 稳定性保障

- 单例锁防止多实例运行
- 渲染进程崩溃后 3 秒自动重建主窗口
- 显示器/DPI 变更防抖监听（250ms）
- 操作超时保护（截图 60s，批注 5min）
- 外部关闭兜底（Alt+F4/系统关机）

### 性能预算

| 指标 | 目标值 |
|------|--------|
| 空闲内存 | ≤ 220MB（标准档）/ ≤ 150MB（节能档） |
| 冷启动至可交互 | ≤ 3s |
| 触控延迟 | ≤ 120ms（中位数） |
| 窗口重定位 | ≤ 200ms |
| 安装包体积 | ≤ 110MB |

## 系统要求

- **操作系统**：Windows 10/11（64位）
- **内存**：≥ 4GB RAM
- **Node.js**：≥ 20.0.0
- **屏幕分辨率**：1366×768 ~ 3840×2160（16:9 / 16:10）
- **系统缩放**：100% ~ 250%

## 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建功能分支：`git checkout -b feat/xxx`
3. 提交更改：`git commit -m "feat: xxx"`
4. 推送分支：`git push origin feat/xxx`
5. 创建 Pull Request

## 许可证

[Apache-2.0](LICENSE) © 2026 ClassTechStar

---

<p align="center">Made with ❤️ for teachers and students</p>

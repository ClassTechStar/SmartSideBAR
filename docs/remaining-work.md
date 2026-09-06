# SmartSideBAR（Avalonia 版）遗留工作清单

> 生成于 2026-09-06，MLP 方案（优化方案_Master_MLP_v1.md）Wave A/B/C 已交付后的
> 未完成项汇总。按优先级分组；勾选框便于逐项销账。
> 当前基线：avalonia 分支 @ 01b0d44，安装包 `dist/SmartSideBAR-2.0.0-waveC-win-x64.zip`
> （已签名，签名者 CN=Seewo Sidekick Team，DigiCert RFC3161 时间戳）。

## P0 · 发布前必办

- [ ] **真机人工回归矩阵**（docs/testing.md）：V1-V8 AppBar 场景全表；
      6 主链路 × 100%/125%/150%/250% 缩放；希沃触屏真机（Pointer 批注/悬浮球拖拽/侧栏点击）。
- [ ] **录屏端到端产物校验**：真实录制 ≥1 分钟 → 确认 MP4 可被希沃白板/微信读取、
      时长与帧率正确、StopAsync 收尾完整（moov box）；多显示器场景选屏录制未实现（当前固定主屏）。
- [ ] **长截图真机回归**：滚动页（PDF/网页/希沃课件）拼接质量、到底检测、取消路径。
- [ ] **_NSIS 实机打包_**：本机无 makensis，`installer/SmartSideBAR.nsi` 未实测；
      CI（choco install nsis）路径需跑通一次并人工安装验证。
- [ ] **Explorer 重启清理验证**：dock 调试期 taskkill 强杀在 shell 中泄漏了死 hwnd 的
      AppBar 带（dock 方块落位偏左 78px 的根因）；需重启 Explorer/重启电脑确认清理后
      方块贴齐右缘。

## P1 · 功能补全（Wave D 收官）

- [ ] 录屏**麦克风混流**（ADR-M4 增强）：`config.recorder.mic` 已有配置项，未接入管线。
- [ ] 录屏**多显示器选屏**：`GraphicsCaptureItemProbe` 固定主屏，需枚举 HMONITOR + 选择 UI。
- [ ] **自动隐藏 AppBar**（K6/P2）：ABM_SETAUTOHIDEBAR 注册 + 鼠标贴边唤出。
- [ ] 悬浮球**闲时点击穿透**（`floatball:setClickThrough` → WS_EX_TRANSPARENT）与
      位置持久化（拖拽结束写回 `config.floatBall.x/y`）。
- [ ] 悬浮球**IME 徽标 / 录屏状态环**（v1.1 的 fb-ime-badge / fb-rec-ring）。
- [ ] **提醒面板 UI**（reminder:add 等通道后端就绪，无前端界面；铃声预设选择 UI 缺）。
- [ ] **USB/打印机/任务管理器面板**（v1.1 为侧栏内嵌面板；当前为 Toast + 独立动作）。
- [ ] **设置窗口补全**：OOBE 角色裁剪联动设置项；外观（液态玻璃参数）编辑；
      热键冲突替代建议展示（HotkeyService.GetSlots 已有数据）。
- [ ] **通知中心**（notification:show/dismiss 通道：D12 v1.2.0 空实现，当前仅 Toast，无 dismiss）。
- [ ] 批注**触屏压力笔宽**（Pointer 压感 → 笔宽）与高亮笔类型。
- [ ] **诊断包安装器集成**：设置界面导出后引导打开目录。

## P2 · 质量工程（Wave D 门禁）

- [ ] **覆盖率门禁 0 → 80%**（CI Coverage gate 阈值待提升；当前 75 用例）。
- [ ] `dotnet format --verify-no-changes` 门禁接入 CI（§7.2）。
- [ ] **性能优化**（§8 实测未达标项）：
  - [ ] 空闲内存 私有 87.5MB → ≤80MB（WinRT/GraphicsCapture 句柄持有审查、Avalonia 渲染线程）。
  - [ ] 冷启动 ~3.5s → ≤1.2s（单文件解压为首帧大头：ReadyToRun / 改多文件分发 / 延迟加载）。
- [ ] AppBar 自适应计时（V2 ≤200ms）自动化基准。
- [ ] E2E 冒烟脚本化（SSB_SMOKE_EXIT_MS / SSB_AUTO_DOCK_MS / SSB_AUTO_UNDOCK_MS 已就绪，
      需接入 CI 定期跑 + 崩溃截图归档）。

## P3 · 运营与演进（Wave E）

- [ ] **v2.0-beta Release 流程**：打 tag → CI release job（签名走 CODESIGN_PFX_B64/
      CODESIGN_PFX_PASS secrets——注意仓库内 pfx-password.txt 是 DPAPI 串，CI 需明文口令
      单独入 secret）→ NSIS 产物 → GitHub Release 草稿。
- [ ] **旧版迁移公告**：Electron v1.2.0 → Avalonia 版切换说明（配置互认已实现，
      `%APPDATA%\SmartSideBAR\config.json` + 旧目录自动迁移）。
- [ ] **Electron 版归档**：FE-PARITY 验收通过后归档（附录 A 76 通道逐行勾验仍欠）。
- [ ] 通道映射余项：`display:list/sidebarTarget`（IDisplayService 未建）、`oobe:closeAndOpenMain`
      OOBE 完成后主窗联动、`reminder:selectSound/playTest`（文件选择 UI）、
      `appearance:get/set/changed`（AppearanceService 未建，玻璃参数当前为静态浅色主题）。
- [ ] MSIX 可选通道评估（ADR-M7）。

## 已知技术勘误记录（实现与方案的偏差，详见 docs/mlp-wave-a-report.md）

1. AppBar Reposition 锚点 rcMonitor（非方案 §5.3 的 rcWork，防占位反蚀漂移）。
2. WndProc 钩子用 SetWindowLongPtr 子类化（Avalonia AddWndProcCallback 非公开 API）。
3. AppBar 全部 Win32 调用包 PhysicalDpiScope（宿主线程 DPI 上下文不确定）。
4. DBT_DEVTYP_VOLUME 不能 RegisterDeviceNotification 过滤注册（err=13）；
   USB 检测 = 全量 WM_DEVICECHANGE 广播 + 驱动器差异扫描。
5. 19041 投影 GraphicsCaptureItem 无 CreateForMonitorAsync →
   IGraphicsCaptureItemInterop COM 互操作。
6. 透明窗口经外部 MoveWindow 缩放后 Avalonia 不出帧 → dock 用独立 DockWindow。
7. Avalonia 命中测试要求可命中元素：透明画布交互挂宿主 Border(Transparent)。
8. 项目名含 .Windows/.Avalonia 时命名空间前缀遮蔽 —— 跨框架类型一律 global:: 别名。

## 环境备忘

- NuGet 走 nuget.azure.cn 镜像（慢）；TFM = net10.0-windows10.0.19041.0。
- `cert/pfx-password.txt` 内容为 **DPAPI 串**（ConvertFrom-SecureString 产物），
  非明文口令——签名脚本需先 `ConvertTo-SecureString` 解密（Git Bash 下注意
  signtool 参数需 `MSYS2_ARG_CONV_EXCL="*"` 防路径转换）。
- 冒烟钩子：`SSB_SMOKE_EXIT_MS` / `SSB_AUTO_DOCK_MS` / `SSB_AUTO_UNDOCK_MS`。
- 本机无 makensis/Inno；NSIS 产物依赖 CI。

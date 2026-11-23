# ADR-M4: 录屏走 Windows.Graphics.Capture + MediaFoundation 最小闭环

- **状态**: 已定
- **日期**: 2026-10
- **关联**: `src/SmartSideBAR.Windows/Recording/GraphicsCaptureRecorder.cs`

## 背景

需要在不装第三方运行库、不随包分发 FFmpeg 的前提下录 MP4。目标机器是 4–8GB 的教学一体机，可能是骁龙 ARM 上跑 x64 仿真。

## 决定

1. 采集用 `Windows.Graphics.Capture`（Win10 1803+），编码用 MediaFoundation H.264，容器 MP4。
2. 音频轨只在有真实采样源时才声明。**没有麦克风采样源就不声明 AAC 轨**——只声明不给数据会让转码器立即 EOF，产出 0 字节文件（B9 回归）。
3. 硬编默认开启，失败自动回退软编；x64 真机走能力探测。
4. 帧池缓冲 3 帧（原 2 帧高负载会丢帧）。
5. 提供暂停/继续；暂停段不计入成片时间线。

## 代价与未做

- 麦克风混流仍未实现（需要 AudioGraph / MediaStreamSource 一路音频源），设置页开关已禁用并如实标注。
- 骁龙 ARM 的 x64 仿真会导致 MediaFoundation 转码崩溃，根因解决方案是提供 win-arm64 原生构建（CI 已产出双架构）。

## 变更记录

| 日期 | 变更 |
|------|------|
| 2026-10 | 初版：GraphicsCapture + MF 最小闭环 |
| 2026-11 | 帧池 2→3、硬编探测回退、暂停/继续、arm64 原生产物 |

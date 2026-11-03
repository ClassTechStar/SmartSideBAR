// 演示模式 (比赛/公开课现场零失误保障): SSB_DEMO=1 启动。
// 生效范围: 一切"自动重启"类守护 (内存看门狗) 豁免 —— 现场绝不允许应用自己重启自己;
// 其余功能全量开放, 保证评委看到的与真实使用完全一致。
namespace SmartSideBAR.Core.Diagnostics;

public static class DemoMode
{
    /// <summary>SSB_DEMO=1 时启用。</summary>
    public static bool Enabled { get; } =
        Environment.GetEnvironmentVariable("SSB_DEMO") == "1";
}

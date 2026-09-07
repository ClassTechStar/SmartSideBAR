// Windows/Audio/SoundService.cs —— 提醒铃声: winmm 直调 (PlaySound 别名 + MCI 播 MP3)。
// 替代 v1.2.0 的 PowerShell SystemSounds 子进程; 消费 reminderSound 全字段 (音量/重复/间隔)。
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.Audio;

public interface ISoundService
{
    /// <summary>异步播放; 重复次数与间隔来自配置。返回播放任务 (fire-and-forget 亦可)。</summary>
    Task PlayReminderAsync(ReminderSoundConfig cfg, CancellationToken ct = default);
}

public sealed class SoundService(ILogger<SoundService>? log = null) : ISoundService
{
    private static readonly Dictionary<string, string> PresetAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = "SystemAsterisk",
        ["bell"] = "SystemAsterisk",
        ["chime"] = "SystemHand",
        ["alarm"] = "SystemExclamation",
    };

    public async Task PlayReminderAsync(ReminderSoundConfig cfg, CancellationToken ct = default)
    {
        var repeat = Math.Clamp(cfg.Repeat, 1, 5);
        for (var i = 0; i < repeat; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (cfg.Preset == "custom" && !string.IsNullOrEmpty(cfg.Mp3Path) && File.Exists(cfg.Mp3Path))
            {
                PlayMp3(cfg.Mp3Path, cfg.Volume);
            }
            else
            {
                var alias = PresetAliases.GetValueOrDefault(cfg.Preset, "SystemAsterisk");
                _ = Winmm.PlaySound(alias, 0, Winmm.SND_ALIAS | Winmm.SND_ASYNC);
            }
            if (i < repeat - 1)
            {
                await Task.Delay(Math.Max(100, cfg.RepeatInterval), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>MCI 播放 MP3 (音量 0~1 → 0~1000); fire-and-forget, 结束自动关闭别名。</summary>
    private void PlayMp3(string path, double volume)
    {
        var alias = "ssbReminder";
        var open = Winmm.mciSendString($"open \"{path}\" type mpegvideo alias {alias}", null, 0, 0);
        if (open != 0)
        {
            log?.LogWarning("[Sound] MCI open 失败 ({Code}): {Path}", open, path);
            return;
        }
        var vol = Math.Clamp(volume, 0, 1);
        _ = Winmm.mciSendString($"setaudio {alias} volume to {(int)(vol * 1000)}", null, 0, 0);
        _ = Winmm.mciSendString($"play {alias}", null, 0, 0);
        // 播放时长未知: 5s 兜底关闭 (课堂铃声场景足够)
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(5000).ConfigureAwait(false);
                Winmm.mciSendString($"close {alias}", null, 0, 0);
            }
            catch { /* 尽力而为 */ }
        });
    }
}

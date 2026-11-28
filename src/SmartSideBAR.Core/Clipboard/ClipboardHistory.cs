// 剪贴板历史纯逻辑 (方案 §5.3, W3-4) —— 存储/去重/容量/隐私闸门全部可单测。
// 隐私三闸 (教学/机房工具, 红线从严):
//   1. 总开关默认关, 面板显式开启;
//   2. 敏感进程黑名单: 前台进程命中本条不记录;
//   3. 暂停记录 + 退出清空。
// 粘贴回用只回写剪贴板, 自动合成按键默认关 (注入敏感面, 设置可开并标注)。
namespace SmartSideBAR.Core.Clipboard;

public enum ClipboardEntryKind { Text, Files }

public sealed record ClipboardEntry(
    string Id,
    ClipboardEntryKind Kind,
    string Text,                 // 文本内容; 文件条目 = 换行拼接的路径列表
    IReadOnlyList<string> Paths, // 文件条目的路径列表 (文本条目为空)
    DateTimeOffset Time,
    bool Pinned = false);

public sealed record ClipboardConfig
{
    /// <summary>总开关。关闭后监听完全不记录 (隐私红线 1)。</summary>
    public bool Enabled { get; init; }
    /// <summary>文本条目上限 (环形)。</summary>
    public int MaxTextEntries { get; init; } = 200;
    /// <summary>文件条目上限。</summary>
    public int MaxFileEntries { get; init; } = 20;
    /// <summary>敏感进程黑名单 (进程名小写): 前台命中时本条不记录 (隐私红线 2)。</summary>
    public List<string> SensitiveProcesses { get; init; } = ["mstsc", "credialoghost", "bitlocker"];
    /// <summary>自动向前台合成 Ctrl+V (注入敏感面, 默认只回写剪贴板)。</summary>
    public bool AutoPaste { get; init; }
}

public static class ClipboardHistoryRules
{
    public const int MaxTextChars = 4096;
    public const int MaxFileEntries = 20;

    /// <summary>是否应该采集本条: 总开关开、未暂停、前台进程不在黑名单。</summary>
    public static bool ShouldCapture(bool enabled, bool paused, string? foregroundProcess, IEnumerable<string> blacklist)
    {
        if (!enabled || paused) return false;
        if (string.IsNullOrEmpty(foregroundProcess)) return true;
        return !blacklist.Contains(foregroundProcess.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>文本规整: 去首尾空白 + 4KB 截断; 过短 (单字符重复粘贴噪音) 仍允许。</summary>
    public static string NormalizeText(string text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim()[..Math.Min(text.Trim().Length, MaxTextChars)];
}

/// <summary>剪贴板历史存储: 去重置顶 + 分类环形容量 + Pin。</summary>
public sealed class ClipboardHistoryStore
{
    private readonly object _gate = new();
    private readonly List<ClipboardEntry> _text = [];   // 最新在前
    private readonly List<ClipboardEntry> _files = [];
    private int _seq;
    private bool _paused;
    private DateTimeOffset _pauseUntil;

    public event Action? Changed;

    public bool Paused => _paused;

    /// <summary>隐私红线 3: 暂停记录 60s (输密码场景), 可再次调用延长。</summary>
    public void Pause(int seconds = 60)
    {
        _paused = true;
        _pauseUntil = DateTimeOffset.Now.AddSeconds(seconds);
    }

    public void Resume()
    {
        _paused = false;
        _pauseUntil = DateTimeOffset.MinValue;
    }

    /// <summary>调用方已过 ShouldCapture 闸门; 去重 (同内容置顶)、容量环形。</summary>
    public void Add(ClipboardEntryKind kind, string text, IReadOnlyList<string> paths)
    {
        lock (_gate)
        {
            var target = kind == ClipboardEntryKind.Files ? _files : _text;
            var max = kind == ClipboardEntryKind.Files ? 20 : 200;
            var existing = target.FindIndex(e =>
                string.Equals(e.Text, text, StringComparison.Ordinal));
            var id = existing >= 0 ? target[existing].Id : (++_seq).ToString("D6");
            if (existing >= 0) target.RemoveAt(existing);
            var entry = new ClipboardEntry(id, kind, text, paths, DateTimeOffset.Now);
            // Pin 项始终留在原位, 新条目插到第一个非 Pin 之前
            var insertAt = target.FindIndex(e => !e.Pinned);
            if (insertAt < 0) insertAt = target.Count;
            target.Insert(Math.Min(insertAt, target.Count), entry);
            // 容量回收: 从尾部 (最旧的非 Pin) 淘汰
            var overflow = target.Count - max;
            for (var i = target.Count - 1; i >= 0 && overflow > 0; i--)
            {
                if (target[i].Pinned) continue;
                target.RemoveAt(i);
                overflow--;
            }
        }
        Changed?.Invoke();
    }

    public IReadOnlyList<ClipboardEntry> Texts() { lock (_gate) return _text.ToArray(); }
    public IReadOnlyList<ClipboardEntry> Files() { lock (_gate) return _files.ToArray(); }

    public void Remove(string id)
    {
        lock (_gate)
        {
            _text.RemoveAll(e => e.Id == id);
            _files.RemoveAll(e => e.Id == id);
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_gate) { _text.Clear(); _files.Clear(); }
        Changed?.Invoke();
    }
}

// 离线墨迹转文字 —— Windows.Media.Ocr (WinRT, 本机引擎, 无网络无云端)。
// 差异化叙事: "离线墨迹智能" = 形状识别 (纯几何) + 文字识别 (系统 OCR) 双引擎,
// 课堂数据不出机房。与 ICC 的 WinRT InkAnalyzer 不同, 这里走"笔画渲染成位图 → OCR"
// 的降耦合路线: 不依赖 InkPresenter, Avalonia 渲染管线直接复用。
using Windows.Globalization;
using Windows.Media.Ocr;

namespace SmartSideBAR.Windows.Ink;

public sealed record InkTextResult(string Text);

public static class InkTextService
{
    private static OcrEngine? _engine;
    private static bool _probed;

    /// <summary>系统是否装有可用 OCR 语言包 (中文教学机默认具备)。</summary>
    public static bool IsSupported
    {
        get
        {
            EnsureEngine();
            return _engine is not null;
        }
    }

    private static OcrEngine? EnsureEngine()
    {
        if (_probed) return _engine;
        _probed = true;
        _engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? OcrEngine.TryCreateFromLanguage(new Language("zh-Hans-CN"))
            ?? OcrEngine.TryCreateFromLanguage(new Language("zh-CN"))
            ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"));
        return _engine;
    }

    /// <summary>识别 PNG 字节流中的手写/印刷文字。不可用或无文字返回 null/空文本。</summary>
    public static async Task<InkTextResult?> RecognizeAsync(byte[] pngBytes)
    {
        var engine = EnsureEngine();
        if (engine is null || pngBytes.Length == 0) return null;
        using var stream = new MemoryStream(pngBytes).AsRandomAccessStream();
        var decoder = await global::Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();
        var result = await engine.RecognizeAsync(bitmap);
        var text = string.Join(Environment.NewLine, result.Lines.Select(l => l.Text));
        return new InkTextResult(text);
    }
}

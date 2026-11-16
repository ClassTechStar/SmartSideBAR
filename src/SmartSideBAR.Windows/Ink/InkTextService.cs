// 离线墨迹转文字 (双引擎)。
// P1-8 主路线: WinRT InkAnalyzer 手写识别 —— InkStrokeBuilder 直接从点阵构造 InkStroke,
// 不依赖 InkPresenter, 手写识别质量较印刷体 OCR 是数量级差异;
// 回退路线: "笔画渲染成位图 → Windows.Media.Ocr" (识别器缺失/异常时兜底)。
// 差异化叙事不变: "离线墨迹智能" = 形状识别 (纯几何) + 文字识别 (本机引擎), 课堂数据不出机房。
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

    /// <summary>
    /// 墨迹转文字主入口 (P1-8): 先走 WinRT InkAnalyzer 手写识别;
    /// 识别器不可用或无结果时回退「笔画渲染位图 → 系统 OCR」。均无结果返回 null/空文本。
    /// </summary>
    /// <param name="strokes">笔画点阵 (每笔一个点序列, DIP 坐标)。</param>
    /// <param name="pngFallback">同组笔画的白底 PNG (调用方渲染, 供 OCR 回退)。</param>
    public static async Task<InkTextResult?> RecognizeInkAsync(
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> strokes, byte[]? pngFallback)
    {
        var inkText = await TryInkAnalyzerAsync(strokes).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(inkText)) return new InkTextResult(inkText);
        return pngFallback is { Length: > 0 } ? await RecognizeAsync(pngFallback) : null;
    }

    /// <summary>WinRT InkAnalyzer 手写识别; 失败/无结果返回 null (由调用方回退 OCR)。</summary>
    private static async Task<string?> TryInkAnalyzerAsync(IReadOnlyList<IReadOnlyList<(double X, double Y)>> strokes)
    {
        try
        {
            if (strokes.Count == 0) return null;
            var analyzer = new global::Windows.UI.Input.Inking.Analysis.InkAnalyzer();
            var builder = new global::Windows.UI.Input.Inking.InkStrokeBuilder();
            var added = 0;
            foreach (var pts in strokes)
            {
                if (pts.Count < 2) continue;
                // InkStrokeBuilder 直接从点阵构造 InkStroke (不依赖 InkPresenter)
                var stroke = builder.CreateStroke(
                    pts.Select(p => new global::Windows.Foundation.Point(p.X, p.Y)));
                analyzer.AddDataForStroke(stroke);
                added++;
            }
            if (added == 0) return null;
            await analyzer.AnalyzeAsync().AsTask().ConfigureAwait(false);
            var words = new List<string>();
            CollectWords(analyzer.AnalysisRoot, words);
            if (words.Count == 0) return null;
            // 拉丁词间留空格; 中文字词间不加空格
            return System.Text.RegularExpressions.Regex.Replace(
                string.Join(" ", words), @"(?<=[\u4e00-\u9fff])\s+(?=[\u4e00-\u9fff])", "");
        }
        catch
        {
            return null;
        }
    }

    private static void CollectWords(global::Windows.UI.Input.Inking.Analysis.IInkAnalysisNode node, List<string> words)
    {
        if (node is global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkWord word)
        {
            if (!string.IsNullOrWhiteSpace(word.RecognizedText)) words.Add(word.RecognizedText);
            return;
        }
        foreach (var child in node.Children) CollectWords(child, words);
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

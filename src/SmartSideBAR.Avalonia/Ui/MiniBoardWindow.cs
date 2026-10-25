// 板中板 (ICC MiniWhiteboardWindow 模式): 独立浮动的临时小黑板/小白板。
// 与全屏批注互补 —— 全屏批注覆盖在课件上讲, 板中板是"临时演算纸"。
// P1-8 后工具条/引擎与全屏批注共享 (Ui/Ink/)。白板/黑板双底色 (ICC 配色)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SmartSideBAR.Windows.Native;
using AloLayout = global::Avalonia.Layout;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class MiniBoardWindow : Window
{
    private static readonly Color WhiteBoard = Color.FromRgb(234, 235, 237);
    private static readonly Color BlackBoard = Color.FromRgb(22, 41, 36);

    private readonly InkCanvas _ink = new();
    private readonly Border _boardSurface;
    private readonly string _dir;
    private bool _darkBoard;
    private TextBlock _boardLabel = null!;

    public MiniBoardWindow(string dir, bool inkToShape = true)
    {
        _dir = dir;

        Topmost = true;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        CanResize = false;
        ShowActivated = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];
        Width = 860;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.Manual;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ---- 头部: 拖动区 + 板面切换 + 保存/关闭
        _boardLabel = new TextBlock
        {
            Text = "小白板",
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF)),
            VerticalAlignment = AloLayout.VerticalAlignment.Center,
        };
        var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto") };
        headerGrid.Children.Add(new TextBlock
        {
            Text = "板中板",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = AloLayout.VerticalAlignment.Center,
        });
        Grid.SetColumn(_boardLabel, 1);
        headerGrid.Children.Add(_boardLabel);
        var boardToggle = HeaderBtn("黑板 ⇄", ToggleBoard);
        Grid.SetColumn(boardToggle, 2);
        headerGrid.Children.Add(boardToggle);
        var saveBtn = HeaderBtn("保存", Save);
        Grid.SetColumn(saveBtn, 3);
        headerGrid.Children.Add(saveBtn);
        var closeBtn = HeaderBtn("✕", Close);
        Grid.SetColumn(closeBtn, 4);
        headerGrid.Children.Add(closeBtn);

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x1A, 0x22)),
            Padding = new Thickness(12, 8),
            Child = headerGrid,
        };
        // 头部任意空白处拖动窗口 (frameless 标配)
        header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        // ---- 板面: 底色 + 墨迹 + 指针捕获层
        _ink.HorizontalAlignment = AloLayout.HorizontalAlignment.Stretch;
        _ink.VerticalAlignment = AloLayout.VerticalAlignment.Stretch;
        var inkHost = new Border
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = AloLayout.HorizontalAlignment.Stretch,
            VerticalAlignment = AloLayout.VerticalAlignment.Stretch,
        };
        inkHost.PointerPressed += (_, e) =>
        {
            var p = e.GetCurrentPoint(inkHost);
            if (!p.Properties.IsLeftButtonPressed) return;
            _ink.BeginStroke(p.Position, e.Pointer, inkHost);
            e.Handled = true;
        };
        inkHost.PointerMoved += (_, e) =>
        {
            if (e.Pointer.Captured != inkHost) return;
            _ink.ExtendStroke(e.GetCurrentPoint(inkHost).Position);
            e.Handled = true;
        };
        inkHost.PointerReleased += (_, e) =>
        {
            if (e.Pointer.Captured != inkHost) return;
            _ink.EndStroke();
            e.Pointer.Capture(null);
            e.Handled = true;
        };
        _boardSurface = new Border
        {
            Background = new SolidColorBrush(WhiteBoard),
            Child = new Panel { Children = { _ink, inkHost } },
            ClipToBounds = true,
        };
        Grid.SetRow(_boardSurface, 1);
        grid.Children.Add(_boardSurface);

        // ---- 共享两级工具条 (默认 5 项 + ⋯ 展开; 板中板无「存笔迹」双保存)
        var toolbar = new AnnotateToolbar(_ink,
            save: Save,
            close: Close,
            saveInkOnly: null,
            defaultColorIndex: 5,           // 小白板出厂配黑笔 (色板末位 #111111)
            inkToShape: inkToShape);
        var toolbarHost = new StackPanel
        {
            Orientation = AloLayout.Orientation.Horizontal,
            HorizontalAlignment = AloLayout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 6),
            Children = { toolbar.Host },
        };
        Grid.SetRow(toolbarHost, 2);
        grid.Children.Add(toolbarHost);

        Content = grid;
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) Close();
        };
    }

    /// <summary>白板 ↔ 黑板: 换底色 + 同化墨色自动换对比色。</summary>
    private void ToggleBoard()
    {
        _darkBoard = !_darkBoard;
        _boardSurface.Background = new SolidColorBrush(_darkBoard ? BlackBoard : WhiteBoard);
        _boardLabel.Text = _darkBoard ? "小黑板" : "小白板";
        // 墨色与新底色同化时自动换墨 (白板配黑字 / 黑板配白字)
        var darkInk = IsDark(_ink.PeekColor());
        if (_darkBoard && darkInk) _ink.SetColor(Colors.White);
        if (!_darkBoard && !darkInk) _ink.SetColor(Color.Parse("#111111"));
    }

    private static bool IsDark(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B < 128;

    private void Save()
    {
        try
        {
            var path = Path.Combine(_dir, $"miniboard_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            Directory.CreateDirectory(_dir);
            var scale = Win32Display.GetScaling(TryGetPlatformHandle()?.Handle ?? 0);
            if (scale <= 0) scale = 1.0;
            var w = (int)(_boardSurface.Bounds.Width * scale);
            var h = (int)(_boardSurface.Bounds.Height * scale);
            if (w <= 0 || h <= 0) return;

            using var strokes = _ink.ExportStrokesOnly(w, h, scale);
            using var composite = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            using (var ctx = composite.CreateDrawingContext())
            {
                ctx.FillRectangle(new SolidColorBrush(_darkBoard ? BlackBoard : WhiteBoard), new Rect(0, 0, w, h));
                ctx.DrawImage(strokes, new Rect(0, 0, w, h));
            }
            composite.Save(path);
            ToastService.Instance.Show($"板书已保存: {path}", "success");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"板书保存失败: {ex.Message}", "error");
        }
    }

    private static Button HeaderBtn(string label, Action act) => new()
    {
        Content = label,
        Padding = new Thickness(8, 3),
        FontSize = 12,
        Foreground = Brushes.White,
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
    };
}

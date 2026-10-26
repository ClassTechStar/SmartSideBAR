// 批注工具条 (P1-8/P1-9), AnnotateWindow 与 MiniBoardWindow 共享, 两级化:
//   默认级 (Jobs 裁决「粉笔原则」): 笔 / 擦 / 撤销 / 存 / 退出 + 「⋯ 更多」, 6 项;
//   展开级: 全部 8 工具 + 6 色 + 3 粗细 + 转形开关 + 清空 + (仅笔迹保存)。
// 选中态 = accent 描边 (KoBar native-calm: 填充/描边变化, 不发光)。
// 宿主窗口只放 Host 并在需要时调用 Rebuild; 切换级时由本类自换内容。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AloLayout = global::Avalonia.Layout;

namespace SmartSideBAR.Avalonia.Ui;

internal sealed class AnnotateToolbar
{
    private static readonly Color AccentEdge = Color.Parse("#4D8DF7");

    private readonly InkCanvas _ink;
    private readonly Action _save;
    private readonly Action _close;
    private readonly Action? _saveInkOnly; // 仅全屏批注有 (板中板只有单一带底保存)
    private readonly int _defaultColorIndex;

    private readonly List<Button> _toolButtons = [];
    private readonly List<Button> _minimalToolButtons = [];
    private readonly List<Button> _colorButtons = [];
    private readonly List<Button> _widthButtons = [];
    private Button? _shapeBtn;
    private bool _inkToShapeOn;
    private Color _lastColor;

    public readonly Border Host = new()
    {
        Background = new SolidColorBrush(Color.FromArgb(230, 20, 26, 34)),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(10, 6),
    };

    /// <summary>是否处于展开级 (全工具)。</summary>
    public bool Expanded { get; private set; }

    public AnnotateToolbar(InkCanvas ink, Action save, Action close, Action? saveInkOnly = null,
        int defaultColorIndex = 1, bool inkToShape = true)
    {
        _ink = ink;
        _save = save;
        _close = close;
        _saveInkOnly = saveInkOnly;
        _defaultColorIndex = defaultColorIndex;
        _inkToShapeOn = inkToShape;
        _ink.InkToShape = inkToShape;
        _lastColor = PaletteHex[defaultColorIndex];
        Rebuild();
    }

    public static readonly Color[] PaletteHex =
        [Color.Parse("#E53935"), Color.Parse("#2B6EE0"), Color.Parse("#27AE60"), Color.Parse("#FBC02D"), Color.Parse("#FFFFFF"), Color.Parse("#111111")];

    public void Rebuild()
    {
        _toolButtons.Clear();
        _colorButtons.Clear();
        _widthButtons.Clear();
        _minimalToolButtons.Clear();
        Host.Child = Expanded ? BuildExpanded() : BuildMinimal();
    }

    private void ToggleExpanded()
    {
        Expanded = !Expanded;
        Rebuild();
    }

    // ---- 默认级: 笔 / 擦 / 撤销 / 存 / 退出 / ⋯ ----

    private Control BuildMinimal()
    {
        var panel = new StackPanel
        {
            Orientation = AloLayout.Orientation.Horizontal,
            Spacing = 6,
        };

        Button pen = null!, erase = null!;
        pen = ToolBtn("笔", () => SelectMinimalTool(AnnotateTool.Pen, pen, erase), minWidth: 34);
        erase = ToolBtn("擦", () => SelectMinimalTool(AnnotateTool.Eraser, pen, erase), minWidth: 34);
                // 默认级进来总是笔 (上一会话若在展开级用了形状, 收起即回到笔)
        _ink.SetTool(AnnotateTool.Pen);
        pen.BorderBrush = new SolidColorBrush(AccentEdge);
        _minimalToolButtons.AddRange([pen, erase]);
        panel.Children.Add(pen);
        panel.Children.Add(erase);

        // 当前墨色圆点 (只读指示, 换色进「更多」)
        panel.Children.Add(ColorDot());
        panel.Children.Add(Separator());

        panel.Children.Add(ActionBtn("撤销", () => _ink.Undo()));
        panel.Children.Add(ActionBtn("存", _save));
        panel.Children.Add(ActionBtn("退出", _close));
        panel.Children.Add(ToolBtn("⋯ 更多", ToggleExpanded, minWidth: 46));
        return panel;
    }

    private void SelectMinimalTool(AnnotateTool tool, Button pen, Button erase)
    {
        _ink.SetTool(tool);
        var active = tool == AnnotateTool.Pen ? pen : erase;
        foreach (var b in _minimalToolButtons)
        {
            b.BorderBrush = ReferenceEquals(b, active)
                ? new SolidColorBrush(AccentEdge)
                : Brushes.Transparent;
        }
    }

    private Control ColorDot() => new Border
    {
        Width = 14,
        Height = 14,
        CornerRadius = new CornerRadius(7),
        Background = new SolidColorBrush(_ink.PeekColor()),
        VerticalAlignment = AloLayout.VerticalAlignment.Center,
        Margin = new Thickness(2, 0),
        IsHitTestVisible = false,
    };

    // ---- 展开级: 全工具 ----

    private Control BuildExpanded()
    {
        var panel = new StackPanel
        {
            Orientation = AloLayout.Orientation.Horizontal,
            Spacing = 6,
        };

        (AnnotateTool Tool, string Label)[] tools =
        [
            (AnnotateTool.Pen, "笔"), (AnnotateTool.Highlighter, "荧"), (AnnotateTool.Laser, "激光"),
            (AnnotateTool.Line, "线"), (AnnotateTool.Arrow, "箭"), (AnnotateTool.Rect, "框"),
            (AnnotateTool.Ellipse, "圆"), (AnnotateTool.Eraser, "擦"),
        ];
        var currentTool = AnnotateTool.Pen;
        foreach (var (tool, label) in tools)
        {
            var t = tool;
            Button btn = null!;
            btn = ToolBtn(label, () =>
            {
                _ink.SetTool(t);
                MarkActive(_toolButtons, btn);
            });
            _toolButtons.Add(btn);
            panel.Children.Add(btn);
        }
        if (_toolButtons.Count > 0) MarkActive(_toolButtons, _toolButtons[0]);

        panel.Children.Add(Separator());

        for (var i = 0; i < PaletteHex.Length; i++)
        {
            var c = PaletteHex[i];
            var btn = new Button
            {
                Width = 26,
                Height = 26,
                Background = new SolidColorBrush(c),
                CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
            };
            btn.Click += (_, _) =>
            {
                _ink.SetColor(c);
                _lastColor = c;
                MarkActive(_colorButtons, btn);
            };
            _colorButtons.Add(btn);
            panel.Children.Add(btn);
        }
        if (_colorButtons.Count > _defaultColorIndex) MarkActive(_colorButtons, _colorButtons[_defaultColorIndex]);
        _ink.SetColor(PaletteHex[_defaultColorIndex]);

        panel.Children.Add(Separator());

        foreach (var (label, w) in new[] { ("细", 2.0), ("中", 5.0), ("粗", 10.0) })
        {
            var width = w;
            Button btn = null!;
            btn = ToolBtn(label, () =>
            {
                _ink.SetWidth(width);
                MarkActive(_widthButtons, btn);
            }, minWidth: 34);
            _widthButtons.Add(btn);
            panel.Children.Add(btn);
        }
        if (_widthButtons.Count > 1) MarkActive(_widthButtons, _widthButtons[1]);

        // 转形开关: 选中 = accent 描边
        _shapeBtn = ToolBtn("转形", () =>
        {
            _inkToShapeOn = !_inkToShapeOn;
            _ink.InkToShape = _inkToShapeOn;
            _shapeBtn!.BorderBrush = _inkToShapeOn ? new SolidColorBrush(AccentEdge) : Brushes.Transparent;
        }, minWidth: 38);
        _shapeBtn.BorderBrush = _inkToShapeOn ? new SolidColorBrush(AccentEdge) : Brushes.Transparent;
        panel.Children.Add(_shapeBtn);

        panel.Children.Add(Separator());

        panel.Children.Add(ActionBtn("撤销", () => _ink.Undo()));
        panel.Children.Add(ActionBtn("清空", _ink.Clear));
        if (_saveInkOnly is not null) panel.Children.Add(ActionBtn("存笔迹", _saveInkOnly));
        panel.Children.Add(ActionBtn("存", _save));
        panel.Children.Add(ActionBtn("退出", _close));
        panel.Children.Add(ToolBtn("收起 ⋯", ToggleExpanded, minWidth: 52));
        return panel;
    }

    // ---- 控件工厂 ----

    private static Control Separator() => new Border
    {
        Width = 1,
        Height = 20,
        Margin = new Thickness(4, 0),
        Background = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
    };

    private static Button ToolBtn(string label, Action act, double minWidth = 30) => new()
    {
        Content = label,
        Padding = new Thickness(6, 4),
        MinWidth = minWidth,
        FontSize = 12,
        Foreground = Brushes.White,
        BorderThickness = new Thickness(1.5),
        BorderBrush = Brushes.Transparent,
    };

    private static Button ActionBtn(string label, Action act) => new()
    {
        Content = label,
        Padding = new Thickness(8, 4),
        FontSize = 12,
        Foreground = Brushes.White,
    };

    private static void MarkActive(List<Button> group, Button active)
    {
        foreach (var b in group)
        {
            b.BorderBrush = ReferenceEquals(b, active)
                ? new SolidColorBrush(AccentEdge)
                : Brushes.Transparent;
        }
    }
}

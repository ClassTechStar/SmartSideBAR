// 液态玻璃高光。v1.2 用 CSS 变量 --lg-mx/--lg-my 驱动"高光跟随光标",
// 并用 SVG feDisplacementMap 做折射色散。Avalonia 侧没有 CSS 变量与 backdrop 采样，
// 这里用两层手段做出同样的效果:
//   1) 镜面高光：径向渐变中心跟随光标在控件内的归一化位置（对应 --lg-mx/--lg-my）；
//   2) 色散边：R/G/B 三条极淡的内描边按光标方向错位 1~2 px（对应 aberration）。
// 真正的逐像素折射需要回读窗口背后画面（DWM 合成结果），代价远高于收益，此处不做。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace SmartSideBAR.Avalonia.Ui;

internal sealed class GlassSheen
{
    private readonly Canvas _layer;
    private readonly Rectangle _spot;
    private readonly Border[] _rims;
    private readonly double _aberration;
    private readonly double _specular;

    public GlassSheen(Canvas layer, Palette palette, double cornerRadius, double aberration = 2, double specular = 0.55)
    {
        _layer = layer;
        _aberration = Math.Clamp(aberration, 0, 8);
        _specular = Math.Clamp(specular, 0, 1);

        _spot = new Rectangle { IsHitTestVisible = false };
        _rims =
        [
            Rim(Color.FromArgb(0x12, 0xFF, 0x6E, 0x6E), cornerRadius),
            Rim(Color.FromArgb(0x10, 0x7A, 0xFF, 0x9E), cornerRadius),
            Rim(Color.FromArgb(0x12, 0x6E, 0xB0, 0xFF), cornerRadius),
        ];

        layer.Children.Add(_spot);
        foreach (var rim in _rims) layer.Children.Add(rim);

        Apply(palette);
        layer.SizeChanged += (_, _) => SyncSize();
        SyncSize();
        Track(0.32, 0.08); // 初始光源在左上，与 v1.2 的 18% 0% 一致
    }

    private static Border Rim(Color color, double radius) => new()
    {
        CornerRadius = new CornerRadius(radius),
        BorderThickness = new Thickness(1.2),
        BorderBrush = new SolidColorBrush(color),
        IsHitTestVisible = false,
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
    };

    /// <summary>主题变化时刷新高光颜色（深色底需要更弱的白色叠加）。</summary>
    public void Apply(Palette palette)
    {
        // 白色高光在近白胶囊上几乎不可见（实测上下半区亮度差 <1.5），
        // 改用品牌色低透明度光斑: 既保留"光随指针走", 白底上又看得出来。
        var accent = palette.Accent;
        var core = (byte)(palette.Dark ? 0x1E : 0x1A);
        var mid = (byte)(core * 0.45);
        var edge = palette.Dark
            ? Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF);

        _spot.Fill = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.6, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(core, accent.R, accent.G, accent.B), 0),
                new GradientStop(Color.FromArgb(mid, accent.R, accent.G, accent.B), 0.42),
                new GradientStop(edge, 0.72),
                new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1),
            },
        };
    }

    /// <summary>Canvas 只按子元素 DesiredSize 摆放、不拉伸，必须显式给尺寸，
    /// 否则光斑与描边都是 0×0, 画面上看不见。</summary>
    private void SyncSize()
    {
        var w = _layer.Bounds.Width;
        var h = _layer.Bounds.Height;
        if (w <= 0 || h <= 0) return;
        _spot.Width = w;
        _spot.Height = h;
        foreach (var rim in _rims)
        {
            rim.Width = w;
            rim.Height = h;
            Canvas.SetLeft(rim, 0);
            Canvas.SetTop(rim, 0);
        }
    }

    /// <summary>光标在控件内的归一化位置 (-1~1)，驱动高光中心与色散边错位。</summary>
    public void Track(double nx, double ny)
    {
        nx = Math.Clamp(nx, -1, 1);
        ny = Math.Clamp(ny, -1, 1);

        SyncSize();
        var w = _layer.Bounds.Width;
        var h = _layer.Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // 光斑固定在中心绘制, 用平移把它带走; 直接改 RadialGradientBrush.Center
        // 不会让已渲染的笔刷失效（实测画面完全不动），必须改几何变换。
        _spot.RenderTransform = new TranslateTransform(nx * w * 0.26, ny * h * 0.30);

        var shift = _aberration * 0.6;
        var offsets = new[]
        {
            new Thickness(-nx * shift, -ny * shift, nx * shift, ny * shift),
            new Thickness(0),
            new Thickness(nx * shift, ny * shift, -nx * shift, -ny * shift),
        };
        for (var i = 0; i < _rims.Length; i++) _rims[i].Margin = offsets[i];
    }
}

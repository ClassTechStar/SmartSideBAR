// 墨迹转文字结果浮层卡: 识别文本 + 一键复制。全屏批注与板中板共用。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SmartSideBAR.Avalonia.Ui;

internal sealed class InkTextCard : Border
{
    private readonly TextBlock _text;

    public InkTextCard()
    {
        _text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = Brushes.White,
            MaxWidth = 380,
        };
        var close = new Button
        {
            Content = "×",
            FontSize = 14,
            Padding = new Thickness(6, 0),
            Foreground = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Top,
        };
        close.Click += (_, _) => IsVisible = false;

        var copy = new Button
        {
            Content = "复制文本",
            FontSize = 12,
            Padding = new Thickness(10, 3),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#4D8DF7")),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        copy.Click += async (_, _) =>
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.Clipboard is not null && !string.IsNullOrEmpty(_text.Text))
            {
                await top.Clipboard.SetTextAsync(_text.Text);
                copy.Content = "已复制 ✓";
                await Task.Delay(1200);
                copy.Content = "复制文本";
            }
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(_text);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        Child = new StackPanel
        {
            Spacing = 8,
            Children = { header, copy },
        };
        Background = new SolidColorBrush(Color.FromArgb(235, 20, 26, 34));
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(12, 10);
        BoxShadow = BoxShadows.Parse("0 8 24 #40000000");
        IsVisible = false;
        MaxWidth = 430;
    }

    public void Show(string text)
    {
        _text.Text = text;
        IsVisible = true;
    }
}

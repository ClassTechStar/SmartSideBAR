// M3: 快捷链接面板 VM（分类页签 + 正方形卡片网格 + 内联新增）。
// 数据仍来自 ConfigService.links, 保存走 LinksService（读取过滤视图 / 全量回写语义见 Core）。
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui.Panels.ViewModels;

internal sealed record LinkTab(string Name, bool IsActive, ICommand Command);

internal sealed record LinkTile(string Name, string Letter, bool IsAdd, ICommand Command);

internal sealed class LinksPanelViewModel : PanelViewModel
{
    private static readonly string[] KnownTabs = ["全部", "应用", "网站", "工具", "AI"];

    private readonly Action<LinkItem> _open;
    private string _tab = "全部";
    private string? _lastTab;
    private bool _adding;
    private string? _newName;
    private string? _newUrl;
    private int _categoryIndex = 1;

    public LinksPanelViewModel(SidebarServices services, Palette palette, Action<LinkItem> open)
        : base(services, palette)
    {
        _open = open;
        ToggleAddCommand = new RelayCommand(_ =>
        {
            Adding = !Adding;
            RequestRefresh();
        });
        SubmitCommand = new RelayCommand(_ => Submit());
        CancelCommand = new RelayCommand(_ =>
        {
            Adding = false;
            RequestRefresh();
        });
    }

    public ObservableCollection<LinkTab> Tabs { get; } = [];

    public ObservableCollection<LinkTile> Tiles { get; } = [];

    public string[] Categories { get; } = [.. KnownTabs.Skip(1)];

    public bool Adding
    {
        get => _adding;
        private set => SetProperty(ref _adding, value);
    }

    public bool IsEmpty { get; private set; }

    public string? NewName
    {
        get => _newName;
        set => SetProperty(ref _newName, value);
    }

    public string? NewUrl
    {
        get => _newUrl;
        set => SetProperty(ref _newUrl, value);
    }

    public int CategoryIndex
    {
        get => _categoryIndex;
        set => SetProperty(ref _categoryIndex, value);
    }

    /// <summary>P2-3: 分类切换方向（0 = 首次构建, 不动画）。</summary>
    public int EnterDirection { get; private set; }

    public IImage? EmptyIcon => SvgIcons.Load("link", Palette.Dark);

    public RelayCommand ToggleAddCommand { get; }

    public RelayCommand SubmitCommand { get; }

    public RelayCommand CancelCommand { get; }

    public override void Reload()
    {
        var links = Services.Config.Current.Links
            .Where(l => l.Enabled && !string.IsNullOrWhiteSpace(l.Url))
            .ToList();

        // 页签变化 → 记下方向，供 OnBuilt 播放入场动画
        EnterDirection = _lastTab is null ? 0
            : string.Compare(_tab, _lastTab, StringComparison.Ordinal) >= 0 ? 1 : -1;
        _lastTab = _tab;

        BuildTabs(links);
        BuildTiles(links);

        IsEmpty = links.Count == 0 && !Adding;
        Raise(nameof(IsEmpty));
        Raise(nameof(EmptyIcon));
    }

    /// <summary>页签: 已知分类 ∪ 数据里出现过的自定义分类; 「全部」恒在首位。</summary>
    private void BuildTabs(List<LinkItem> links)
    {
        var custom = links.Select(l => string.IsNullOrWhiteSpace(l.Category) ? "网站" : l.Category.Trim())
            .Where(c => !KnownTabs.Contains(c, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var names = new List<string> { "全部" };
        names.AddRange(KnownTabs.Skip(1));
        names.AddRange(custom);

        Tabs.Clear();
        foreach (var name in names)
        {
            var value = name;
            Tabs.Add(new LinkTab(
                value,
                string.Equals(_tab, value, StringComparison.OrdinalIgnoreCase),
                new RelayCommand(_ => SelectTab(value))));
        }
    }

    /// <summary>正方形图标卡片 + 末尾的新增入口（虚线卡片, KoBar add-button 语义）。</summary>
    private void BuildTiles(List<LinkItem> links)
    {
        var visible = links.Where(l => _tab == "全部"
            || string.Equals(
                string.IsNullOrWhiteSpace(l.Category) ? "网站" : l.Category.Trim(),
                _tab, StringComparison.OrdinalIgnoreCase)).ToList();

        Tiles.Clear();
        foreach (var link in visible)
        {
            var letter = string.IsNullOrWhiteSpace(link.Name) ? "?" : link.Name.Trim()[0].ToString();
            var target = link;
            Tiles.Add(new LinkTile(link.Name, letter, false, new RelayCommand(_ => _open(target))));
        }
        Tiles.Add(new LinkTile("＋ 添加", "", true, ToggleAddCommand));
    }

    private void SelectTab(string name)
    {
        if (string.Equals(_tab, name, StringComparison.Ordinal)) return;
        _tab = name;
        RequestRefresh();
    }

    /// <summary>内联新增: 名称 + URL + 分类, 校验后落盘并刷新列表。</summary>
    private void Submit()
    {
        var n = NewName?.Trim() ?? "";
        var u = NewUrl?.Trim() ?? "";
        if (n.Length == 0 || u.Length == 0)
        {
            ToastService.Instance.Show("名称和网址都要填", "warn");
            return;
        }
        if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            u = "https://" + u;
        }
        try
        {
            var category = Categories.ElementAtOrDefault(CategoryIndex) ?? "网站";
            Services.Links.Add(n, u, category: category);
            Adding = false;
            NewName = null;
            NewUrl = null;
            CategoryIndex = 1;
            RequestRefresh();
            ToastService.Instance.Show($"已添加「{n}」", "success");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"添加失败: {ex.Message}", "error");
        }
    }
}

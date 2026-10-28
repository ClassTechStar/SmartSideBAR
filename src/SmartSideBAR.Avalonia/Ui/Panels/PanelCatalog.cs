// 面板目录 (P2-1 接线)。
// 把原来 ShowPanel 里的 switch 换成「注册表 + 工厂」：
// - 元数据（id / 名称 / 图标 / 顺序 / 是否启用）登记在 Core 的 PanelRegistry；
// - 构造方式登记在本地工厂表；
// 新增一个面板 = 注册一条定义 + 一个工厂，不再改窗口的 switch。
// 借鉴 DiffiTouch 的 contents.py（@load_content 注册）与 CyreneCompass 的 items 树。
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Panels;

namespace SmartSideBAR.Avalonia.Ui.Panels;

/// <summary>面板构造上下文（避免每个工厂都重复解引用服务）。</summary>
internal sealed record PanelContext(
    SidebarServices Services,
    Palette Palette,
    Action<LinkItem> OpenLink);

internal static class PanelCatalog
{
    private static readonly PanelRegistry Registry = new();
    private static readonly Dictionary<PanelKind, Func<PanelContext, SidebarPanel>> Factories = new();
    private static readonly Dictionary<PanelKind, string> Ids = new();
    private static bool _initialized;

    /// <summary>面板 id（与 PanelRegistry 中的定义一致）。</summary>
    public static string IdOf(PanelKind kind)
        => Ids.TryGetValue(kind, out var id) ? id : kind.ToString().ToLowerInvariant();

    /// <summary>注册一个面板：元数据 + 工厂。</summary>
    public static void Register(
        PanelKind kind, PanelDefinition definition, Func<PanelContext, SidebarPanel> factory)
    {
        Ids[kind] = definition.Id;
        Registry.Register(definition);
        Factories[kind] = factory;
    }

    /// <summary>按 kind 创建面板；未注册或定义被禁用时返回 null。</summary>
    public static SidebarPanel? Create(PanelKind kind, PanelContext context)
    {
        EnsureInitialized();
        if (!Factories.TryGetValue(kind, out var factory)) return null;
        var definition = Registry.Get(IdOf(kind));
        if (definition is { Enabled: false }) return null;
        return factory(context);
    }

    /// <summary>当前可见的面板定义（按 Order 排序，已应用禁用名单）。</summary>
    public static IReadOnlyList<PanelDefinition> Visible(IEnumerable<string>? disabled)
    {
        EnsureInitialized();
        return Registry.Visible(disabled);
    }

    /// <summary>登记内置面板（幂等）。</summary>
    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        Register(PanelKind.Capture,
            new PanelDefinition("capture", "截图与批注", "fluent:camera", Order: 1),
            c => new CapturePanel(c.Services, c.Palette));
        Register(PanelKind.Usb,
            new PanelDefinition("usb", "U 盘", "fluent:usb-plug", Order: 2),
            c => new UsbPanel(c.Services, c.Palette));
        Register(PanelKind.Printer,
            new PanelDefinition("printer", "打印机", "fluent:print", Order: 3),
            c => new PrinterPanel(c.Services, c.Palette));
        Register(PanelKind.Links,
            new PanelDefinition("links", "快捷链接", "fluent:link", Order: 4),
            c => new LinksPanel(c.Services, c.Palette, c.OpenLink));
        Register(PanelKind.Reminder,
            new PanelDefinition("reminder", "提醒与通知", "fluent:alert", Order: 5),
            c => new ReminderPanel(c.Services, c.Palette));
        Register(PanelKind.TaskMgr,
            new PanelDefinition("taskmgr", "任务管理", "fluent:task-list", Order: 6),
            c => new TaskMgrPanel(c.Services, c.Palette));
        Register(PanelKind.Settings,
            new PanelDefinition("settings", "设置", "fluent:settings", Order: 7),
            c => new SettingsPanel(c.Services, c.Palette));
        Register(PanelKind.QuickSettings,
            new PanelDefinition("quickSettings", "快捷设置", "fluent:toggle", Order: 8),
            c => new QuickSettingsPanel(c.Services, c.Palette));
    }
}

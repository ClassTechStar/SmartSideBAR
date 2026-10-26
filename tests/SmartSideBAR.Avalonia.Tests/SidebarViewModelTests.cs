// 侧栏 ViewModel 的 Headless 测试（P1-1）。
// 用例原本集中在 Core 纯逻辑与 Windows 替身层, VM 层零覆盖,
// "点了 rail 图标却没切面板""4 个截图图标一起高亮"这类缺陷只能靠肉眼在真机发现。
// 这里直接驱动 SidebarViewModel：不创建真实窗口，只验证状态机与命令路由。
using System.ComponentModel;
using Avalonia.Headless.XUnit;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Links;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Audio;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Native;
using SmartSideBAR.Windows.Recording;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia.Tests;

internal sealed class RecordingBus : IEventBus
{
    public List<object> Events { get; } = [];

    public IDisposable Subscribe<T>(Action<T> handler) => new Noop();

    public void Publish<T>(T evt) => Events.Add(evt!);

    private sealed class Noop : IDisposable
    {
        public void Dispose() { }
    }
}

internal sealed class FakeHook : IWndProcHook
{
    public void Add(nint hwnd, WndProcCallback callback) { }
    public void Remove(nint hwnd, WndProcCallback callback) { }
}

internal sealed class FakeIme : IImeService
{
    public bool Chinese { get; private set; } = true;
    public int ToggleCount { get; private set; }

    public ImeState GetState() => new("Microsoft Pinyin", Chinese);

    public bool Toggle()
    {
        Chinese = !Chinese;
        ToggleCount++;
        return true;
    }

    public bool SelectChinese() => Chinese = true;

    public bool SelectEnglish() => Chinese = false;
}

internal sealed class FakeShell : IShellService
{
    public List<string> Opened { get; } = [];
    public string? Revealed { get; private set; }

    public void OpenTaskManager() => Opened.Add("taskmgr");
    public void OpenExternal(Uri url) => Opened.Add(url.ToString());
    public void OpenPath(string path) => Opened.Add(path);
    public void RevealInExplorer(string path) => Revealed = path;
    public void SetAutoLaunch(bool enable) { }
    public bool GetAutoLaunch() => false;
}

internal sealed class FakeUsb : IUsbWatcher
{
    public void Start(nint hostHwnd) { }
    public void Stop() { }
    public IReadOnlyList<UsbDriveInfo> ListRemovable() => [];
}

internal sealed class FakePrinter : IPrinterMonitor
{
    public void Start() { }
    public void Stop() { }
    public IReadOnlyList<PrinterInfo> Query() => [];
}

internal sealed class FakeRecorder : IRecorderApi
{
    public bool IsRecording { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public bool IsSupported() => true;
    public Task StartAsync(RecorderOptions opt, CancellationToken ct = default) => Task.CompletedTask;
    public Task<string?> StopAsync() => Task.FromResult<string?>(null);
}

internal sealed class FakeSound : ISoundService
{
    public int PlayCount { get; private set; }

    public Task PlayReminderAsync(ReminderSoundConfig cfg, CancellationToken ct = default)
    {
        PlayCount++;
        return Task.CompletedTask;
    }
}

/// <summary>最小 AppBar API 替身：记录被请求的宽度，供"开面板是否请求展开几何"断言。</summary>
internal sealed class FakeAppBarApi : IAppBarApi
{
    public List<int> RequestedWidths { get; } = [];

    public bool Register(nint hwnd, uint callbackMsg) => true;
    public void Remove(nint hwnd) { }
    public AppBarRect QueryAndSetPos(nint hwnd, uint edge, AppBarRect want)
    {
        RequestedWidths.Add(want.W);
        return want;
    }
    public void Activate(nint hwnd) { }
    public void WindowPosChanged(nint hwnd) { }
    public bool MoveWindow(nint hwnd, AppBarRect rect) => true;
    public TaskbarInfo? GetTaskbarPos() => null;
    public MonitorRects GetMonitorOf(nint hwnd) =>
        new(new AppBarRect(0, 0, 2560, 1600), new AppBarRect(0, 0, 2560, 1600));
    public bool SetAutoHideBar(nint hwnd, uint edge, bool enable) => true;
}

public sealed class SidebarViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-vm-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly ConfigService _config;
    private readonly FakeIme _ime = new();
    private readonly FakeShell _shell = new();
    private readonly RecordingBus _bus = new();
    private readonly List<string> _dispatched = [];

    public SidebarViewModelTests()
    {
        var paths = new ConfigPaths(
            UserConfigPath: Path.Combine(_dir, "config.json"),
            ProgramDataConfigPath: Path.Combine(_dir, "pd", "policy.json"),
            LegacyUserConfigPath: null,
            LegacyProgramDataConfigPath: null);
        _config = new ConfigService(paths);
        _config.Load();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    /// <summary>
    /// 关面板的收尾是 Background 优先级延后执行的（避免在控件事件里销毁可视树），
    /// 测试里必须把队列跑空才能观察到状态。
    /// </summary>
    private static void Pump() => global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private SidebarViewModel CreateVm()
    {
        var appBar = new AppBarService(new FakeAppBarApi(), new FakeHook(), _bus);
        var services = new SidebarServices(
            _config, appBar, _ime, _shell, new FakeUsb(), new FakePrinter(),
            new SchedulerService(_config, _bus, TimeProvider.System),
            new FakeRecorder(), new FakeSound(),
            new LinksService(_config),
            action => _dispatched.Add(action));
        return new SidebarViewModel(services, _config.Current.Links);
    }

    [AvaloniaFact]
    public void Rail_has_expected_items_and_ime_badge()
    {
        var vm = CreateVm();

        // 输入法 + 截图组 4 + USB + 打印机 + 任务管理 + 链接 + 提醒 + 设置
        Assert.Equal(11, vm.RailItems.Count);
        Assert.Equal("中", vm.RailItems[0].Badge);
        Assert.All(vm.RailItems, item => Assert.NotNull(item.Command));
    }

    [AvaloniaFact]
    public void Policy_disabled_modules_are_hidden_from_rail()
    {
        _config.Set("policy.disabledModules", new[] { "usb", "printer" });
        var vm = CreateVm();

        Assert.DoesNotContain(vm.RailItems, i => i.Module == "usb");
        Assert.DoesNotContain(vm.RailItems, i => i.Module == "printer");
        Assert.Contains(vm.RailItems, i => i.Module == "capture");
    }

    [AvaloniaFact]
    public void Opening_panel_sets_title_and_state()
    {
        var vm = CreateVm();
        Assert.False(vm.PanelOpen);

        vm.OpenPanel(PanelKind.Reminder);

        Assert.True(vm.PanelOpen);
        Assert.Equal("提醒与通知", vm.PanelTitle);
    }

    [AvaloniaFact]
    public void Only_the_clicked_rail_item_is_highlighted()
    {
        // 截图组 4 个条目共享 Capture 面板：按面板类型判高亮会一次点亮 4 个
        var vm = CreateVm();
        var captureItems = vm.RailItems.Where(i => i.Kind == PanelKind.Capture).ToList();
        Assert.Equal(4, captureItems.Count);

        captureItems[2].Command!.Execute(null);

        Assert.True(captureItems[2].IsActive);
        Assert.Equal(1, vm.RailItems.Count(i => i.IsActive));
    }

    [AvaloniaFact]
    public void Ime_item_toggles_and_does_not_open_panel()
    {
        var vm = CreateVm();

        vm.RailItems[0].Command!.Execute(null);

        Assert.Equal(1, _ime.ToggleCount);
        Assert.False(vm.PanelOpen);
        Assert.Equal("EN", vm.RailItems[0].Badge);
    }

    [AvaloniaFact]
    public void Task_manager_item_opens_panel_instead_of_firing_immediately()
    {
        // v1.2 语义：rail 图标只负责开面板，真正的动作在面板里点（避免误触直接弹窗）
        var vm = CreateVm();

        vm.RailItems.First(i => i.Module == "taskmgr").Command!.Execute(null);

        Assert.True(vm.PanelOpen);
        Assert.Equal("任务管理", vm.PanelTitle);
        Assert.DoesNotContain("taskmgr", _dispatched);
    }

    [AvaloniaFact]
    public void Closing_panel_clears_title_and_highlight()
    {
        var vm = CreateVm();
        vm.RailItems.First(i => i.Module == "usb").Command!.Execute(null);
        Assert.True(vm.PanelOpen);

        vm.ClosePanel();
        Pump();

        Assert.False(vm.PanelOpen);
        Assert.Equal(string.Empty, vm.PanelTitle);
        Assert.DoesNotContain(vm.RailItems, i => i.IsActive);
    }

    [AvaloniaFact]
    public void Links_list_keeps_only_enabled_entries()
    {
        _config.Set("links", new List<LinkItem>
        {
            new() { Id = "a", Name = "A", Url = "https://a.example.com", Enabled = true },
            new() { Id = "b", Name = "B", Url = "https://b.example.com", Enabled = false },
        });

        var vm = CreateVm();

        Assert.Single(vm.Links);
        Assert.Equal("A", vm.Links[0].Name);
    }

    [AvaloniaFact]
    public void Panel_state_raises_property_changed()
    {
        var vm = CreateVm();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.OpenPanel(PanelKind.Settings);

        Assert.Contains("PanelOpen", changed);
        Assert.Contains("PanelTitle", changed);
        Assert.Contains("ActivePanel", changed);
    }
}

// M2: 设置页 VM 的载入/收集契约, 以及设置窗全分页 headless 加载。
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SmartSideBAR.Avalonia.Ui.Settings;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia.Tests;

public sealed class SettingsPagesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-settings-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly ConfigService _config;

    public SettingsPagesTests()
    {
        Directory.CreateDirectory(_dir);
        _config = new ConfigService(new ConfigPaths(
            UserConfigPath: Path.Combine(_dir, "user.json"),
            ProgramDataConfigPath: Path.Combine(_dir, "pd", "policy.json"),
            LegacyUserConfigPath: null,
            LegacyProgramDataConfigPath: null));
        _config.Load();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    [Fact]
    public void 常规页载入并收集配置键()
    {
        _ = _config.Set("display.sidebarSide", "left");
        _ = _config.Set("display.railTrigger", "edge");
        var host = new RecordingHost();
        var vm = new GeneralSettingsViewModel(new FakeShell(), host);
        vm.LoadFrom(_config.Current);

        Assert.Equal(0, vm.SideIndex);
        Assert.Equal(1, vm.TriggerIndex);
        Assert.False(vm.AutoLaunch); // FakeShell.GetAutoLaunch() = false

        var changes = vm.CollectChanges().ToDictionary(c => c.Path, c => c.Value);
        Assert.True(changes.ContainsKey("display.sidebarSide"));
        Assert.True(changes.ContainsKey("display.railTrigger"));
        Assert.True(changes.ContainsKey("lesson.periods"));
    }

    [Fact]
    public void 常规页课表非法时提示且不写课表()
    {
        var host = new RecordingHost();
        var vm = new GeneralSettingsViewModel(new FakeShell(), host);
        vm.LoadFrom(_config.Current);
        vm.LessonText = "这不是课表";

        var paths = vm.CollectChanges().Select(c => c.Path).ToArray();

        Assert.DoesNotContain("lesson.periods", paths);
        Assert.Contains(host.Toasts, t => t.Level == "warn" && t.Message.Contains("课表"));
    }

    [Fact]
    public void 截图与录屏页收集对应键()
    {
        var capture = new CaptureSettingsViewModel();
        capture.LoadFrom(_config.Current);
        capture.FormatIndex = 1;
        var capturePaths = capture.CollectChanges().Select(c => c.Path).ToArray();
        Assert.Contains("capture.format", capturePaths);
        Assert.Contains("capture.dir", capturePaths);

        var recorder = new RecorderSettingsViewModel();
        recorder.LoadFrom(_config.Current);
        recorder.Enabled = false;
        recorder.Fps = 25;
        var changes = recorder.CollectChanges().ToDictionary(c => c.Path, c => c.Value);
        Assert.False(changes["recorder.enabled"]!.GetValue<bool>());
        Assert.Equal(25, changes["recorder.fps"]!.GetValue<int>());
    }

    [Fact]
    public void 设备与快捷键页收集对应键()
    {
        var devices = new DevicesSettingsViewModel();
        devices.LoadFrom(_config.Current);
        devices.UsbEnabled = false;
        devices.PrinterIntervalSec = 30;
        var devicePaths = devices.CollectChanges().Select(c => c.Path).ToArray();
        Assert.Contains("usb.enabled", devicePaths);
        Assert.Contains("printer.pollIntervalSec", devicePaths);

        var hotkeys = new HotkeysSettingsViewModel();
        hotkeys.LoadFrom(_config.Current);
        hotkeys.Capture = "Ctrl+Alt+1";
        var hotkeyPaths = hotkeys.CollectChanges().Select(c => c.Path).ToArray();
        Assert.Contains("capture.hotkey", hotkeyPaths);
        Assert.Contains("ime.hotkey", hotkeyPaths);
    }

    [Fact]
    public void 外观页载入并回写主题键()
    {
        _ = _config.Set("appearance.theme", "dark");
        _ = _config.Set("appearance.accent", "#12A594");
        var vm = new AppearanceSettingsViewModel();
        vm.LoadFrom(_config.Current);

        Assert.Equal(1, vm.ThemeIndex);
        Assert.Equal(2, vm.AccentIndex);
        Assert.Equal("dark", vm.ThemeValue);
        Assert.Equal("#12A594", vm.AccentValue);

        var paths = vm.CollectChanges().Select(c => c.Path).ToArray();
        Assert.Contains("appearance.theme", paths);
        Assert.Contains("appearance.liquidGlass", paths);
        Assert.Contains("appearance.highContrast", paths);
    }

    [AvaloniaFact]
    public void 设置窗全部分页可打开()
    {
        var window = new SettingsWindow(
            _config, new FakeShell(),
            new HotkeyService(new FakeHook(), new EventBus()),
            new FakeDiagnostics());
        window.Show();
        Pump();

        var host = window.FindControl<ContentControl>("PageHost");
        Assert.NotNull(host);
        for (var i = 0; i < window.Vm.Titles.Count; i++)
        {
            window.Vm.SelectedIndex = i;
            Pump();
            Assert.NotNull(host!.Content);
        }

        window.Close();
    }

    private sealed class RecordingHost : ISettingsHost
    {
        public List<(string Message, string Level)> Toasts { get; } = [];

        public Task<string?> PickSaveJsonAsync(string suggestedFileName) => Task.FromResult<string?>(null);

        public Task<string?> PickOpenJsonAsync() => Task.FromResult<string?>(null);

        public void Toast(string message, string level) => Toasts.Add((message, level));

        public void InstallAndExit(string installerPath) { }
    }

    private sealed class FakeDiagnostics : IDiagnosticsService
    {
        public IReadOnlyDictionary<string, string> RunFullCheck() =>
            new Dictionary<string, string> { ["ok"] = "1" };

        public string ExportPack(string dir) => Path.Combine(dir, "diag.zip");
    }
}

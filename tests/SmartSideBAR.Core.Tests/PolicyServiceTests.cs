// PolicyService: policy.disabledModules 消费语义 (P2-1/P2-3) 的单测。
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Policy;

namespace SmartSideBAR.Core.Tests;

public sealed class PolicyServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-cfg-policy-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ConfigService MakeService()
    {
        var paths = new ConfigPaths(
            UserConfigPath: Path.Combine(_dir, "user", "config.json"),
            ProgramDataConfigPath: Path.Combine(_dir, "pd", "policy.json"),
            LegacyUserConfigPath: null,
            LegacyProgramDataConfigPath: null);
        return new ConfigService(paths);
    }

    [Fact]
    public void 禁用模块大小写不敏感命中()
    {
        var svc = MakeService();
        svc.Load();
        Assert.True(svc.Set("policy.disabledModules", new[] { "USB", "capture" }));
        var policy = new PolicyService(svc);
        Assert.True(policy.IsDisabled("usb"));
        Assert.True(policy.IsDisabled("Capture"));
        Assert.False(policy.IsDisabled("ime"));
        Assert.Equal(["USB", "capture"], policy.DisabledModules());
    }
}

// 面板注册表完整性守卫（借鉴 VeriRandom 的"注册完整性有测试兜底"做法）。
// 这里只验注册元数据，不构造真实面板：一旦新增 PanelKind 却忘了在 PanelCatalog 注册，
// 下面的「每个枚举值都有定义」就会红，避免线上才暴露"面板空白"。
using SmartSideBAR.Avalonia.Ui.Panels;
using SmartSideBAR.Avalonia.ViewModels;

namespace SmartSideBAR.Avalonia.Tests;

public sealed class PanelCatalogTests
{
    [Fact]
    public void 内置面板全部注册()
    {
        PanelCatalog.EnsureInitialized();
        var ids = PanelCatalog.Visible(null).Select(p => p.Id).ToHashSet();

        foreach (var kind in Enum.GetValues<PanelKind>())
        {
            if (kind == PanelKind.None) continue;
            var id = PanelCatalog.IdOf(kind);
            Assert.True(ids.Contains(id), $"PanelKind.{kind} 没有对应的面板定义（id={id}）");
        }
    }

    [Fact]
    public void 可见面板按Order排序()
    {
        PanelCatalog.EnsureInitialized();
        var visible = PanelCatalog.Visible(null);
        Assert.Equal(visible.Count, visible.Select(p => p.Order).Count());
        var orders = visible.Select(p => p.Order).ToArray();
        Assert.Equal(orders.OrderBy(o => o), orders);
    }

    [Fact]
    public void 禁用名单过滤生效()
    {
        PanelCatalog.EnsureInitialized();
        var all = PanelCatalog.Visible(null).Count;
        var filtered = PanelCatalog.Visible(["usb"]).Count;
        Assert.Equal(all - 1, filtered);
    }

    [Fact]
    public void 未注册的种类返回空()
    {
        PanelCatalog.EnsureInitialized();
        // PanelKind.None 没有注册工厂
        Assert.Null(PanelCatalog.Create(PanelKind.None, null!));
    }

    [Fact]
    public void 每个面板定义都有非空id与名称()
    {
        PanelCatalog.EnsureInitialized();
        foreach (var def in PanelCatalog.Visible(null))
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Id));
            Assert.False(string.IsNullOrWhiteSpace(def.Label));
        }
    }
}

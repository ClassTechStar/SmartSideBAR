// Wave B 纯逻辑测试: 打印机映射 (E4 修复) + 热键解析/替代建议 (P2-2)
using SmartSideBAR.Core.Devices;
using SmartSideBAR.Core.Input;

namespace SmartSideBAR.Core.Tests;

public sealed class PrinterStateMapperTests
{
    [Theory]
    [InlineData(4u, (ushort)3, PrinterState.OutOfPaper)]   // NoPaper 优先于 Idle (E4: 不再误判)
    [InlineData(8u, (ushort)3, PrinterState.Jammed)]       // Jammed
    [InlineData(9u, (ushort)3, PrinterState.Offline)]      // Offline
    [InlineData(5u, (ushort)3, PrinterState.LowInk)]       // LowToner
    [InlineData(3u, (ushort)3, PrinterState.LowPaper)]
    [InlineData(7u, (ushort)3, PrinterState.DoorOpen)]
    [InlineData(2u, (ushort)3, PrinterState.Idle)]         // NoError + Idle
    [InlineData(2u, (ushort)4, PrinterState.Ok)]           // 打印中 = 可用
    [InlineData(null, (ushort)7, PrinterState.Offline)]
    [InlineData(null, (ushort)2, PrinterState.Unknown)]
    public void Map_DetectedErrorStateWins(uint? detected, ushort status, PrinterState expected)
    {
        Assert.Equal(expected, PrinterStateMapper.Map(detected, status));
    }

    [Fact]
    public void ToWire_AlignsTypeScriptUnion()
    {
        Assert.Equal("ok", PrinterStateMapper.ToWire(PrinterState.Ok));
        Assert.Equal("ok", PrinterStateMapper.ToWire(PrinterState.Idle));
        Assert.Equal("out_of_paper", PrinterStateMapper.ToWire(PrinterState.OutOfPaper));
        Assert.Equal("low_ink", PrinterStateMapper.ToWire(PrinterState.LowInk));
        Assert.Equal("jammed", PrinterStateMapper.ToWire(PrinterState.Jammed));
        Assert.Equal("offline", PrinterStateMapper.ToWire(PrinterState.Offline));
        Assert.Equal("unknown", PrinterStateMapper.ToWire(PrinterState.Unknown));
    }
}

public sealed class HotkeyParserTests
{
    [Fact]
    public void Parse_CtrlShiftA()
    {
        var ok = HotkeyParser.TryParse("Ctrl+Shift+A", out var combo);
        Assert.True(ok);
        Assert.Equal(HotkeyCombo.ModCtrl | HotkeyCombo.ModShift, combo.Modifiers);
        Assert.Equal((uint)'A', combo.VirtualKey);
        Assert.Equal("Ctrl+Shift+A", combo.ToString());
    }

    [Fact]
    public void Parse_F5_AndSpace()
    {
        Assert.True(HotkeyParser.TryParse("f5", out var f5));
        Assert.Equal(0x74u, f5.VirtualKey);
        Assert.True(HotkeyParser.TryParse("Alt+Space", out var altSpace));
        Assert.Equal(0x20u, altSpace.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl++")]
    [InlineData("Ctrl+Bad!Key")]
    public void Parse_RejectsInvalid(string text)
    {
        Assert.False(HotkeyParser.TryParse(text, out _));
    }

    [Fact]
    public void SuggestAlternatives_NeverReturnsOriginal()
    {
        HotkeyParser.TryParse("Ctrl+Shift+A", out var original);
        var suggestions = HotkeyParser.SuggestAlternatives(original);
        Assert.NotEmpty(suggestions);
        Assert.DoesNotContain(original, suggestions);
        Assert.All(suggestions, s => Assert.True(HotkeyParser.TryParse(s.ToString(), out _)));
    }

    // ---- P2-c 增补: 修饰键全集/回写/替代建议路径 ----

    [Fact]
    public void Parse_WinShiftInsert()
    {
        Assert.True(HotkeyParser.TryParse("Win+Shift+Insert", out var w));
        Assert.Equal(HotkeyCombo.ModWin | HotkeyCombo.ModShift, w.Modifiers);
        Assert.Equal(0x2Du, w.VirtualKey);
    }

    [Fact]
    public void Combo_ToString_兜底与别名()
    {
        Assert.Equal("F5", new HotkeyCombo(0, 0x74).ToString());
        Assert.Equal("Ctrl+Space", new HotkeyCombo(HotkeyCombo.ModCtrl, 0x20).ToString());
        Assert.Equal("Alt+Delete", new HotkeyCombo(HotkeyCombo.ModAlt, 0x2E).ToString());
        Assert.Equal("0x09", new HotkeyCombo(0, 0x09).ToString()); // 未知键十六进制兜底
    }

    [Fact]
    public void Parse_然后回写往返一致()
    {
        Assert.True(HotkeyParser.TryParse("Ctrl+Shift+D", out var combo));
        Assert.Equal("Ctrl+Shift+D", combo.ToString());
    }

    [Fact]
    public void SuggestAlternatives_覆盖换键路径()
    {
        var original = new HotkeyCombo(HotkeyCombo.ModCtrl | HotkeyCombo.ModShift, (uint)'A');
        var list = HotkeyParser.SuggestAlternatives(original);
        Assert.Contains(new HotkeyCombo(HotkeyCombo.ModCtrl | HotkeyCombo.ModShift | HotkeyCombo.ModAlt, (uint)'A'), list); // Ctrl 族加 Alt
        Assert.Contains(new HotkeyCombo(HotkeyCombo.ModCtrl, (uint)'A'), list); // 去 Shift

        var win = HotkeyParser.SuggestAlternatives(new HotkeyCombo(HotkeyCombo.ModWin, (uint)'A'));
        Assert.Contains(new HotkeyCombo(HotkeyCombo.ModCtrl, (uint)'A'), win); // Win → Ctrl

        var f5 = HotkeyParser.SuggestAlternatives(new HotkeyCombo(HotkeyCombo.ModCtrl, 0x74));
        Assert.Contains(new HotkeyCombo(HotkeyCombo.ModCtrl, 0x75), f5); // F5 → F6 邻位
    }
}

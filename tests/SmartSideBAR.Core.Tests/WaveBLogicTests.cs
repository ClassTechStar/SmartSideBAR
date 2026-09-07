// Wave B 纯逻辑测试: 打印机映射 (E4 根修) + 热键解析/替代建议 (P2-2)
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
}

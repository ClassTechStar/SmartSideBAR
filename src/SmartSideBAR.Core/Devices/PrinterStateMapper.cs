// Core/Devices/PrinterStateMapper.cs —— E4 根修 (方案 §6.7): 用 DetectedErrorState 枚举做状态判定。
// 与 v1.2.0 的「PrinterState===3 判 ok」错误映射不同, 此处字段语义逐一对齐 Win32_Printer 文档:
//   DetectedErrorState: 0=Unknown 1=Other 2=NoError 3=LowPaper 4=NoPaper 5=LowToner 6=NoToner
//                       7=DoorOpen 8=Jammed 9=Offline 10=ServiceRequested 11=OutputBinFull
//   PrinterStatus:      1=Other 2=Unknown 3=Idle 4=Printing 5=WarmingUp 6=StoppedPrinting 7=Offline
namespace SmartSideBAR.Core.Devices;

public enum PrinterState
{
    Ok,
    Idle,
    OutOfPaper,
    LowPaper,
    LowInk,
    Jammed,
    Offline,
    DoorOpen,
    ServiceRequired,
    Unknown,
}

public static class PrinterStateMapper
{
    /// <summary>与 types.ts PrinterStatus.state 联合类型对齐的对外字串（camelCase）。</summary>
    public static string ToWire(PrinterState state) => state switch
    {
        PrinterState.Ok => "ok",
        PrinterState.Idle => "ok",          // Idle 亦视为正常可用 (v1.2.0 状态族无 idle)
        PrinterState.OutOfPaper => "out_of_paper",
        PrinterState.LowPaper => "out_of_paper",
        PrinterState.LowInk => "low_ink",
        PrinterState.Jammed => "jammed",
        PrinterState.Offline => "offline",
        PrinterState.DoorOpen => "jammed",  // 门开归入卡纸族提示
        PrinterState.ServiceRequired => "unknown",
        _ => "unknown",
    };

    /// <summary>纯净映射函数 —— 单测锁定语义。</summary>
    public static PrinterState Map(uint? detectedErrorState, ushort? printerStatus)
    {
        switch (detectedErrorState)
        {
            case 3: return PrinterState.LowPaper;
            case 4: return PrinterState.OutOfPaper;
            case 5 or 6: return PrinterState.LowInk;
            case 7: return PrinterState.DoorOpen;
            case 8: return PrinterState.Jammed;
            case 9: return PrinterState.Offline;
            case 10 or 11: return PrinterState.ServiceRequired;
        }
        return printerStatus switch
        {
            3 => PrinterState.Idle,
            4 => PrinterState.Ok,          // 打印中 = 在线可用
            5 => PrinterState.Ok,          // 预热
            7 => PrinterState.Offline,
            2 or null => PrinterState.Unknown,
            _ => PrinterState.Ok,          // 1=Other 等按可用处理, 不误报
        };
    }
}

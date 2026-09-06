// Windows/AppBar/IAppBarApi.cs —— Win32 AppBar API 抽象: 测试注入假实现 (方案 §5.3 / §7.1)
namespace SmartSideBAR.Windows.AppBar;

public enum AppBarEdge { Left, Top, Right, Bottom }

public readonly record struct AppBarRect(int X, int Y, int W, int H)
{
    public static readonly AppBarRect Empty = new(0, 0, 0, 0);
    public readonly int Right => X + W;
    public readonly int Bottom => Y + H;
}

public readonly record struct TaskbarInfo(uint Edge, AppBarRect Rect);

public readonly record struct MonitorRects(AppBarRect Monitor, AppBarRect Work);

public interface IAppBarApi
{
    /// <summary>ABM_NEW —— 返回 false = 与他栏冲突/不可用, 调用方回退 alwaysOnTop。</summary>
    bool Register(nint hwnd, uint callbackMsg);

    /// <summary>ABM_REMOVE —— 未注册时调用无副作用 (孤儿注册自愈利用此点)。</summary>
    void Remove(nint hwnd);

    /// <summary>ABM_QUERYPOS → 按边恢复尺寸 → ABM_SETPOS; 返回系统授予矩形 (语义对齐 appbar.cc)。</summary>
    AppBarRect QueryAndSetPos(nint hwnd, uint edge, AppBarRect want);

    /// <summary>ABM_ACTIVATE —— WM_ACTIVATE 时上报 (K2, 多 AppBar z 序维护)。</summary>
    void Activate(nint hwnd);

    /// <summary>ABM_WINDOWPOSCHANGED —— 位置变更上报 (K2)。</summary>
    void WindowPosChanged(nint hwnd);

    /// <summary>以物理像素直接落位窗口 (K4 单轨: 与系统协商后由本层统一执行)。</summary>
    bool MoveWindow(nint hwnd, AppBarRect rect);

    TaskbarInfo? GetTaskbarPos();

    /// <summary>hwnd 所在显示器的全屏矩形与工作区矩形 (物理像素)。</summary>
    MonitorRects GetMonitorOf(nint hwnd);
}

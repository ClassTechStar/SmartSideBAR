// 悬浮球位置恢复与夹取 (P0-1)。
// 借鉴 CyreneCompass 的 restore_target / clamp_target：
// 恢复位置时先判断记录位置是否完整落在工作区内，否则回到主屏中央并标记 reset;
// 拖到边缘时把目标夹取进工作区，避免压到任务栏或跑出屏幕。
namespace SmartSideBAR.Core.FloatBall;

/// <summary>工作区矩形（物理像素）。</summary>
public readonly record struct WorkAreaRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>恢复结果：坐标 + 是否被重置（调用方据此回写修正后的记录）。</summary>
public readonly record struct RestoredPosition(int X, int Y, bool Reset);

public static class FloatBallPositionPolicy
{
    /// <summary>位置是否完整落在工作区内（含宽高容纳判断）。</summary>
    public static bool Fits(int x, int y, int width, int height, WorkAreaRect area)
    {
        if (area.Width <= 0 || area.Height <= 0) return false;
        return x >= area.Left
            && y >= area.Top
            && (long)x + width <= area.Right
            && (long)y + height <= area.Bottom;
    }

    /// <summary>
    /// 恢复位置：候选工作区能容纳则原样返回；否则回到主屏中央并置 Reset=true。
    /// 宽高大于工作区时按左上角对齐，保证至少可见。
    /// </summary>
    public static RestoredPosition Restore(
        int x, int y, int width, int height, WorkAreaRect? candidate, WorkAreaRect primary)
    {
        if (candidate is { } area && Fits(x, y, width, height, area))
            return new RestoredPosition(x, y, false);

        var cx = primary.Left + Math.Max(0, primary.Width - width) / 2;
        var cy = primary.Top + Math.Max(0, primary.Height - height) / 2;
        return new RestoredPosition(cx, cy, true);
    }

    /// <summary>把目标夹取进工作区（用于拖拽过程中的边界约束）。</summary>
    public static RestoredPosition Clamp(int x, int y, int width, int height, WorkAreaRect area)
    {
        if (area.Width <= 0 || area.Height <= 0) return new RestoredPosition(x, y, false);
        var maxX = Math.Max(area.Left, area.Right - width);
        var maxY = Math.Max(area.Top, area.Bottom - height);
        return new RestoredPosition(
            Math.Clamp(x, area.Left, maxX),
            Math.Clamp(y, area.Top, maxY),
            false);
    }
}

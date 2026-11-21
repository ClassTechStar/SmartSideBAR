// 耗时统计封装（借鉴 Flow.Launcher 的 Stopwatch.Debug/Info）。
// 适配：Flow.Launcher 的封装直接写 NLog，这里把"计时"与"记录"分离 ——
// Core 只产出耗时结构，由调用方决定写日志/发育生指标，便于单测且无第三方依赖。
using System.Diagnostics;

namespace SmartSideBAR.Core.Diagnostics;

/// <summary>一次操作的耗时结果。</summary>
public sealed record TimedResult<T>(T Value, long ElapsedMs)
{
    /// <summary>是否超过阈值（阈值为 0 时不判定）。</summary>
    public bool Exceeds(long thresholdMs) => thresholdMs > 0 && ElapsedMs > thresholdMs;
}

public static class TimedOperation
{
    /// <summary>执行同步操作并返回耗时。</summary>
    public static TimedResult<T> Run<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var sw = Stopwatch.StartNew();
        var value = action();
        sw.Stop();
        return new TimedResult<T>(value, sw.ElapsedMilliseconds);
    }

    /// <summary>执行无返回值操作并返回耗时。</summary>
    public static TimedResult<bool> Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return new TimedResult<bool>(true, sw.ElapsedMilliseconds);
    }

    /// <summary>执行异步操作并返回耗时。</summary>
    public static async Task<TimedResult<T>> RunAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var sw = Stopwatch.StartNew();
        var value = await action().ConfigureAwait(false);
        sw.Stop();
        return new TimedResult<T>(value, sw.ElapsedMilliseconds);
    }

    /// <summary>执行异步无返回值操作并返回耗时。</summary>
    public static async Task<TimedResult<bool>> RunAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var sw = Stopwatch.StartNew();
        await action().ConfigureAwait(false);
        sw.Stop();
        return new TimedResult<bool>(true, sw.ElapsedMilliseconds);
    }
}

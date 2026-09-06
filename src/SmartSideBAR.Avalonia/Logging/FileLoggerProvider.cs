// Wave A 轻量文件日志 —— 按日滚动 (Wave D 按方案换 Serilog 滚动 7d 保留)。
// 约束: 无第三方依赖, 同步写 + 锁 (Wave A 日志量极小, 不构成瓶颈)。
using Microsoft.Extensions.Logging;

namespace SmartSideBAR.Avalonia.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly string _logDir;
    private DateTime _currentDay;
    private StreamWriter? _writer;

    public FileLoggerProvider(string logDir)
    {
        _logDir = logDir;
        Directory.CreateDirectory(logDir);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    private StreamWriter AcquireWriter()
    {
        var today = DateTime.UtcNow.Date;
        if (_writer is null || _currentDay != today)
        {
            _writer?.Dispose();
            _currentDay = today;
            _writer = new StreamWriter(
                Path.Combine(_logDir, $"ssb-{today:yyyyMMdd}.log"), append: true)
            {
                AutoFlush = true,
            };
        }
        return _writer;
    }

    public void Dispose()
    {
        lock (_gate) _writer?.Dispose();
    }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {category}: {formatter(state, exception)}";
            if (exception is not null) line += Environment.NewLine + exception;
            lock (owner._gate)
            {
                try { owner.AcquireWriter().WriteLine(line); }
                catch { /* 日志失败不影响业务 */ }
            }
        }
    }
}

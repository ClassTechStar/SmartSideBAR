// Wave A 轻量文件日志 —— 按日滚动；P2-2 补上保留期与单文件上限。
// 约束: 无第三方依赖, 同步写 + 锁 (日志量极小, 不构成瓶颈)。
// 轮转策略本身在 Core/Diagnostics/LogRotation.cs（纯逻辑、可单测），这里只负责落地。
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Avalonia.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private readonly string _logDir;
    private readonly int _retentionDays;
    private readonly long _maxBytes;
    private DateTime _currentDay;
    private StreamWriter? _writer;
    private long _bytesWritten;

    public FileLoggerProvider(string logDir)
        : this(logDir, LogRotation.DefaultRetentionDays, LogRotation.DefaultMaxBytes)
    {
    }

    public FileLoggerProvider(string logDir, int retentionDays, long maxBytes)
    {
        _logDir = logDir;
        _retentionDays = retentionDays;
        _maxBytes = maxBytes;
        Directory.CreateDirectory(logDir);
        // 启动时先清一轮：长期驻留机可能已积累大量历史文件
        LogRotation.Prune(_logDir, "ssb-*.log", _retentionDays, DateTime.Now);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    private StreamWriter AcquireWriter()
    {
        var today = DateTime.Now.Date;
        if (_writer is not null && _currentDay != today)
        {
            // 跨天：关掉旧文件并清一轮过期日志
            _writer.Dispose();
            _writer = null;
            LogRotation.Prune(_logDir, "ssb-*.log", _retentionDays, DateTime.Now);
        }

        if (_writer is null)
        {
            _currentDay = today;
            _bytesWritten = CurrentFileLength(LogRotation.DailyName(today));
            _writer = new StreamWriter(
                Path.Combine(_logDir, LogRotation.DailyName(today)), append: true)
            {
                AutoFlush = true,
            };
        }
        return _writer;

        long CurrentFileLength(string name)
        {
            try
            {
                var info = new FileInfo(Path.Combine(_logDir, name));
                return info.Exists ? info.Length : 0;
            }
            catch { return 0; }
        }
    }

    /// <summary>单文件到上限：改名归档后重新打开当日文件继续写。</summary>
    private void Roll()
    {
        var archived = Path.Combine(_logDir, LogRotation.RolledName(_currentDay, DateTime.Now));
        _writer?.Dispose();
        _writer = null;
        try
        {
            var current = Path.Combine(_logDir, LogRotation.DailyName(_currentDay));
            if (File.Exists(current)) File.Move(current, archived, overwrite: true);
        }
        catch
        {
            // 归档失败（文件被占用）不影响后续写入
        }
        _bytesWritten = 0;
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
                try
                {
                    var writer = owner.AcquireWriter();
                    writer.WriteLine(line);
                    owner._bytesWritten += line.Length + 2;

                    if (LogRotation.ShouldRoll(owner._bytesWritten, owner._maxBytes))
                    {
                        owner.Roll();
                    }
                }
                catch { /* 日志失败不影响业务 */ }
            }
        }
    }
}

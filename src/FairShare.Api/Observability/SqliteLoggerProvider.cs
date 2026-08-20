using System;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace FairShare.Api.Observability;

/// <summary>A captured log call, queued for the batch writer.</summary>
public readonly record struct LogRow(DateTime OccurredAtUtc, int Level, string Category, string Message, string? Exception);

/// <summary>
/// Hand-rolled provider persisting logs to the app's SQLite database (ADR 0003: no Serilog -
/// the redaction and recursion guarantees live in code we own). Log calls enqueue into a
/// bounded channel and never block or throw; <see cref="LogSink"/> drains it in batches.
/// </summary>
[ProviderAlias("Sqlite")]
public sealed class SqliteLoggerProvider : ILoggerProvider
{
    // Bounded so a log storm degrades to dropped diagnostics, never to memory growth or
    // request latency (NFR-4).
    private readonly Channel<LogRow> _channel = Channel.CreateBounded<LogRow>(
        new BoundedChannelOptions(2048)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });

    private readonly LogLevelSwitch _levelSwitch;

    public SqliteLoggerProvider(LogLevelSwitch levelSwitch)
    {
        _levelSwitch = levelSwitch;
    }

    /// <summary>Drained by <see cref="LogSink"/>; public so tests can observe captures.</summary>
    public ChannelReader<LogRow> Reader => _channel.Reader;

    public ILogger CreateLogger(string categoryName) => new SqliteLogger(categoryName, _levelSwitch, _channel.Writer);

    public void Dispose() => _channel.Writer.TryComplete();

    private sealed class SqliteLogger(string category, LogLevelSwitch levelSwitch, ChannelWriter<LogRow> writer) : ILogger
    {
        private readonly string _category = Truncate(category, LogEntry.MaxCategoryLength);
        private readonly LogLevelSwitch _levelSwitch = levelSwitch;
        private readonly ChannelWriter<LogRow> _writer = writer;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => _levelSwitch.ShouldCapture(_category, logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message;
            try
            {
                message = formatter(state, exception);
            }
            catch
            {
                // A broken formatter must never take the request down with it.
                message = state?.ToString() ?? string.Empty;
            }

            _writer.TryWrite(new LogRow(
                DateTime.UtcNow,
                (int)logLevel,
                _category,
                Truncate(message, LogEntry.MaxMessageLength),
                exception is null ? null : Truncate(exception.ToString(), LogEntry.MaxExceptionLength)));
        }

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    }
}

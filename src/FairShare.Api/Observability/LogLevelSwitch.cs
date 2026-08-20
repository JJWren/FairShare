using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace FairShare.Api.Observability;

/// <summary>
/// Runtime minimum-level switch for the SQLite log sink. Normal capture: FairShare.* at
/// Information, everything else at Warning. Verbose mode drops those to Debug/Information
/// and always turns itself back off: expiry is checked on read, and a process restart
/// resets it (the state is deliberately in-memory only - ADR 0003).
/// </summary>
public class LogLevelSwitch(TimeProvider time)
{
    public static readonly TimeSpan VerboseDuration = TimeSpan.FromHours(4);

    private readonly TimeProvider _time = time;

    // UTC ticks until which verbose mode is active; 0 = off. Interlocked because the
    // admin endpoint writes while every log call site reads.
    private long _verboseUntilUtcTicks;

    public bool VerboseEnabled => Volatile.Read(ref _verboseUntilUtcTicks) > _time.GetUtcNow().UtcTicks;

    public DateTime? VerboseUntilUtc
    {
        get
        {
            long ticks = Volatile.Read(ref _verboseUntilUtcTicks);
            return ticks > _time.GetUtcNow().UtcTicks ? new DateTime(ticks, DateTimeKind.Utc) : null;
        }
    }

    public void EnableVerbose() =>
        Volatile.Write(ref _verboseUntilUtcTicks, _time.GetUtcNow().Add(VerboseDuration).UtcTicks);

    public void DisableVerbose() => Volatile.Write(ref _verboseUntilUtcTicks, 0);

    public bool ShouldCapture(string category, LogLevel level)
    {
        if (level == LogLevel.None)
        {
            return false;
        }

        // EF Core below Warning is both noisy and a recursion hazard (queries issued while
        // reading the log viewer would log themselves); never capture it, verbose or not.
        if (category.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
        {
            return level >= LogLevel.Warning;
        }

        bool isApp = category.StartsWith("FairShare", StringComparison.Ordinal);
        LogLevel minimum = VerboseEnabled
            ? (isApp ? LogLevel.Debug : LogLevel.Information)
            : (isApp ? LogLevel.Information : LogLevel.Warning);

        return level >= minimum;
    }
}

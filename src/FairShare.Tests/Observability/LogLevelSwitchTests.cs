using FairShare.Api.Observability;
using Microsoft.Extensions.Logging;

namespace FairShare.Tests.Observability;

public class LogLevelSwitchTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Normal_CapturesAppInformationButNotDebug()
    {
        LogLevelSwitch sw = new(new FakeTimeProvider(Start));

        Assert.True(sw.ShouldCapture("FairShare.Api.Controllers.AuthController", LogLevel.Information));
        Assert.False(sw.ShouldCapture("FairShare.Api.Controllers.AuthController", LogLevel.Debug));
        Assert.True(sw.ShouldCapture("Microsoft.AspNetCore.Hosting", LogLevel.Warning));
        Assert.False(sw.ShouldCapture("Microsoft.AspNetCore.Hosting", LogLevel.Information));
    }

    [Fact]
    public void Verbose_CapturesAppDebugAndFrameworkInformation()
    {
        LogLevelSwitch sw = new(new FakeTimeProvider(Start));
        sw.EnableVerbose();

        Assert.True(sw.VerboseEnabled);
        Assert.NotNull(sw.VerboseUntilUtc);
        Assert.True(sw.ShouldCapture("FairShare.Api.Observability.AnalyticsService", LogLevel.Debug));
        Assert.True(sw.ShouldCapture("Microsoft.AspNetCore.Hosting", LogLevel.Information));
    }

    [Fact]
    public void Verbose_AlwaysTurnsItselfBackOff()
    {
        FakeTimeProvider time = new(Start);
        LogLevelSwitch sw = new(time);
        sw.EnableVerbose();

        time.Advance(LogLevelSwitch.VerboseDuration + TimeSpan.FromMinutes(1));

        Assert.False(sw.VerboseEnabled);
        Assert.Null(sw.VerboseUntilUtc);
        Assert.False(sw.ShouldCapture("FairShare.Api.X", LogLevel.Debug));
    }

    [Fact]
    public void Verbose_CanBeDisabledManually()
    {
        LogLevelSwitch sw = new(new FakeTimeProvider(Start));
        sw.EnableVerbose();
        sw.DisableVerbose();

        Assert.False(sw.VerboseEnabled);
    }

    [Fact]
    public void EfCoreCategories_NeverCaptureBelowWarning_EvenVerbose()
    {
        LogLevelSwitch sw = new(new FakeTimeProvider(Start));
        sw.EnableVerbose();

        Assert.False(sw.ShouldCapture("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information));
        Assert.True(sw.ShouldCapture("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning));
    }

    [Fact]
    public void Provider_CapturesThroughSwitchAndTruncates()
    {
        LogLevelSwitch sw = new(new FakeTimeProvider(Start));
        using SqliteLoggerProvider provider = new(sw);
        ILogger logger = provider.CreateLogger("FairShare.Tests.Sample");

        logger.LogDebug("dropped in normal mode");
        logger.LogInformation("kept {Value}", new string('x', 5000));

        Assert.True(provider.Reader.TryRead(out LogRow row));
        Assert.Equal((int)LogLevel.Information, row.Level);
        Assert.Equal(LogEntry.MaxMessageLength, row.Message.Length);
        Assert.False(provider.Reader.TryRead(out _));
    }
}

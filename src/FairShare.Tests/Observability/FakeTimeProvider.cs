namespace FairShare.Tests.Observability;

/// <summary>Minimal controllable clock for switch/rollup tests.</summary>
public class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan by) => UtcNow += by;
}

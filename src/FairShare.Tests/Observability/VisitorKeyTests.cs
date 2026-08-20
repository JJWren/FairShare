using FairShare.Api.Observability;

namespace FairShare.Tests.Observability;

public class VisitorKeyTests
{
    private static readonly byte[] Secret = new byte[32];
    private const string Ip = "203.0.113.7";
    private const string Ua = "Mozilla/5.0 test";

    [Fact]
    public void Compute_IsDeterministicWithinOneDay()
    {
        DateOnly day = new(2026, 8, 20);

        Assert.Equal(
            VisitorKey.Compute(Secret, day, Ip, Ua),
            VisitorKey.Compute(Secret, day, Ip, Ua));
    }

    [Fact]
    public void Compute_ChangesAcrossDays_SoVisitorsAreUnlinkable()
    {
        Assert.NotEqual(
            VisitorKey.Compute(Secret, new DateOnly(2026, 8, 20), Ip, Ua),
            VisitorKey.Compute(Secret, new DateOnly(2026, 8, 21), Ip, Ua));
    }

    [Fact]
    public void Compute_ChangesWithIpUaAndSecret()
    {
        DateOnly day = new(2026, 8, 20);
        string baseline = VisitorKey.Compute(Secret, day, Ip, Ua);

        byte[] otherSecret = new byte[32];
        otherSecret[0] = 1;

        Assert.NotEqual(baseline, VisitorKey.Compute(Secret, day, "203.0.113.8", Ua));
        Assert.NotEqual(baseline, VisitorKey.Compute(Secret, day, Ip, "Mozilla/5.0 other"));
        Assert.NotEqual(baseline, VisitorKey.Compute(otherSecret, day, Ip, Ua));
    }

    [Fact]
    public void Compute_ProducesLowercaseHexWithinColumnLimit()
    {
        string key = VisitorKey.Compute(Secret, new DateOnly(2026, 8, 20), Ip, Ua);

        Assert.Equal(64, key.Length);
        Assert.Equal(key, key.ToLowerInvariant());
        Assert.All(key, c => Assert.True(char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f')));
    }
}

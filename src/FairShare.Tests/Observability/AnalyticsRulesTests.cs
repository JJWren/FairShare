using FairShare.Api.Observability;
using Microsoft.AspNetCore.Http;

namespace FairShare.Tests.Observability;

public class AnalyticsRulesTests
{
    private const string RealBrowserUa =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Googlebot/2.1 (+http://www.google.com/bot.html)")]
    [InlineData("Mozilla/5.0 (compatible; bingbot/2.0)")]
    [InlineData("curl/8.4.0")]
    [InlineData("python-requests/2.31")]
    [InlineData("HeadlessChrome/120.0")]
    [InlineData("UptimeMonitor/1.0")]
    public void IsBot_FlagsNonBrowsers(string? userAgent) => Assert.True(AnalyticsRules.IsBot(userAgent));

    [Fact]
    public void IsBot_AllowsRealBrowser() => Assert.False(AnalyticsRules.IsBot(RealBrowserUa));

    [Theory]
    [InlineData("1", null, true)]
    [InlineData(null, "1", true)]
    [InlineData("1", "1", true)]
    [InlineData(null, null, false)]
    [InlineData("0", null, false)]
    public void OptedOut_HonorsDntAndGpc(string? dnt, string? gpc, bool expected)
    {
        HeaderDictionary headers = [];
        if (dnt is not null)
        {
            headers["DNT"] = dnt;
        }

        if (gpc is not null)
        {
            headers["Sec-GPC"] = gpc;
        }

        Assert.Equal(expected, AnalyticsRules.OptedOut(headers));
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/States/AL/CS42", "/states/al/cs42")]
    [InlineData("/states/al/", "/states/al")]
    [InlineData("/profiles?tab=2", "/profiles")]
    [InlineData("/profiles#section", "/profiles")]
    public void TryNormalizePath_AcceptsAndNormalizesRoutes(string raw, string expected)
    {
        Assert.True(AnalyticsRules.TryNormalizePath(raw, out string normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-leading-slash")]
    [InlineData("//protocol-relative")]
    [InlineData("/admin")]
    [InlineData("/admin/stats")]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/account/change-password")]
    [InlineData("/go/donate")]
    [InlineData("/healthz")]
    [InlineData("/api/v1/parents")]
    [InlineData("/_framework/blazor.webassembly.js")]
    [InlineData("/favicon.ico")]
    [InlineData("/feed.xml")]
    public void TryNormalizePath_RejectsUncountablePaths(string? raw) =>
        Assert.False(AnalyticsRules.TryNormalizePath(raw, out _));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("not a url", null)]
    [InlineData("https://www.google.com/search?q=cs42", "www.google.com")]
    [InlineData("https://EXAMPLE.org/page", "example.org")]
    public void NormalizeReferrer_KeepsExternalHostOnly(string? referrer, string? expected) =>
        Assert.Equal(expected, AnalyticsRules.NormalizeReferrer(referrer, "easychildsupport.fyi"));

    [Fact]
    public void NormalizeReferrer_DropsInternalReferrals() =>
        Assert.Null(AnalyticsRules.NormalizeReferrer("https://easychildsupport.fyi/states/al", "easychildsupport.fyi"));

    [Theory]
    [InlineData("gated-hit", "profiles", true)]
    [InlineData("Gated-Hit", "PROFILES", true)]
    [InlineData("gated-hit", null, true)]
    public void TryNormalizeClientEvent_AcceptsWhitelistedEvents(string name, string? target, bool expected)
    {
        Assert.Equal(expected, AnalyticsRules.TryNormalizeClientEvent(name, target, out string normalizedName, out string? normalizedTarget));
        Assert.Equal("gated-hit", normalizedName);
        Assert.Equal(target?.ToLowerInvariant(), normalizedTarget);
    }

    [Theory]
    [InlineData("calculation-completed", null)] // server-observed events cannot be forged by clients
    [InlineData("donate-click", null)]
    [InlineData("custom-event", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("gated-hit", "income $4,200")] // free text can smuggle case content - refused
    [InlineData("gated-hit", "UPPER CASE SPACES")]
    public void TryNormalizeClientEvent_RejectsForgeriesAndFreeText(string? name, string? target) =>
        Assert.False(AnalyticsRules.TryNormalizeClientEvent(name, target, out _, out _));
}

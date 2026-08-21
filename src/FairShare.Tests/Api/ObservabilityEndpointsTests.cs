using System.Net.Http.Headers;
using FairShare.Api.Observability;
using FairShare.Api.Persistence;
using FairShare.Contracts.Auth;
using FairShare.Contracts.Calculation;
using FairShare.Contracts.Observability;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class ObservabilityEndpointsTests : IClassFixture<FairShareApiFactory>
{
    private const string BrowserUa =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private readonly FairShareApiFactory _factory;
    private readonly HttpClient _client;

    public ObservabilityEndpointsTests(FairShareApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        // The bot filter treats a missing UA as a bot; tests impersonate a real browser.
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUa);
    }

    // ---- Beacon: page views -------------------------------------------------------

    [Fact]
    public async Task PageViewBeacon_RecordsAnonymousRow()
    {
        int before = CountPageViews();

        HttpResponseMessage response = await _client.PostAsJsonAsync(
            "api/v1/analytics/page-views",
            new PageViewRequest { Path = "/states/al/cs42", Referrer = "https://www.google.com/search" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();
        PageView row = db.PageViews.OrderByDescending(v => v.Id).First();

        Assert.Equal(before + 1, db.PageViews.Count());
        Assert.Equal("/states/al/cs42", row.Path);
        Assert.Equal("www.google.com", row.ReferrerHost);
        Assert.Equal(64, row.VisitorKey.Length);
        // Nothing identifying is stored: the key is a hash, not the inputs.
        Assert.DoesNotContain("Mozilla", row.VisitorKey);
    }

    [Fact]
    public async Task PageViewBeacon_DoesNotRecord_WhenOptedOutBotOrUncountable()
    {
        int before = CountPageViews();

        HttpRequestMessage dnt = new(HttpMethod.Post, "api/v1/analytics/page-views")
        {
            Content = JsonContent.Create(new PageViewRequest { Path = "/" })
        };
        dnt.Headers.Add("DNT", "1");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(dnt)).StatusCode);

        HttpRequestMessage bot = new(HttpMethod.Post, "api/v1/analytics/page-views")
        {
            Content = JsonContent.Create(new PageViewRequest { Path = "/" })
        };
        bot.Headers.UserAgent.ParseAdd("Googlebot/2.1");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(bot)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.PostAsJsonAsync("api/v1/analytics/page-views", new PageViewRequest { Path = "/admin/stats" })).StatusCode);

        Assert.Equal(before, CountPageViews());
    }

    [Fact]
    public async Task PageViewBeacon_DoesNotRecordAdminsOwnBrowsing()
    {
        int before = CountPageViews();
        string adminToken = await LoginAsAdminAsync();

        HttpRequestMessage request = new(HttpMethod.Post, "api/v1/analytics/page-views")
        {
            Content = JsonContent.Create(new PageViewRequest { Path = "/" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(request)).StatusCode);
        Assert.Equal(before, CountPageViews());
    }

    // ---- Beacon: client events ----------------------------------------------------

    [Fact]
    public async Task EventBeacon_AcceptsGatedHit_ButNeverServerEventNames()
    {
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.PostAsJsonAsync("api/v1/analytics/events", new ClientEventRequest { Name = "gated-hit", Target = "profiles" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.PostAsJsonAsync("api/v1/analytics/events", new ClientEventRequest { Name = "calculation-completed", Target = "al/cs42" })).StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

        Assert.Equal(1, db.AnalyticsEvents.Count(e => e.Name == "gated-hit" && e.Target == "profiles"));
        // The forged server event must not exist (calculations record it server-side only).
        Assert.Equal(0, db.AnalyticsEvents.Count(e => e.Name == "calculation-completed"));
    }

    // ---- Server events from calculations ------------------------------------------

    [Fact]
    public async Task Calculation_RecordsStartedAndCompletedEvents_ForGuests()
    {
        string guestToken = await StartGuestAsync();

        HttpRequestMessage request = new(HttpMethod.Post, "api/v1/states/AL/forms/CS42/calculations")
        {
            Content = JsonContent.Create(new CalculationRequest
            {
                NumberOfChildren = 1,
                Plaintiff = new ParentDataDto { MonthlyGrossIncome = 1200, HealthcareCoverageCosts = 100, HasPrimaryCustody = true },
                Defendant = new ParentDataDto { MonthlyGrossIncome = 1000, WorkRelatedChildcareCosts = 20 }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);

        HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

        Assert.Equal(1, db.AnalyticsEvents.Count(e => e.Name == "calculation-started" && e.Target == "al/cs42"));
        Assert.Equal(1, db.AnalyticsEvents.Count(e => e.Name == "calculation-completed" && e.Target == "al/cs42"));
    }

    // ---- Admin endpoints: auth and behavior ----------------------------------------

    [Fact]
    public async Task AdminObservabilityEndpoints_RequireAdmin()
    {
        string guestToken = await StartGuestAsync();

        foreach (string route in new[]
                 {
                     "api/v1/admin/stats/summary", "api/v1/admin/stats/pages", "api/v1/admin/stats/referrers",
                     "api/v1/admin/stats/activity", "api/v1/admin/logs", "api/v1/admin/logs/audit", "api/v1/admin/logs/verbose"
                 })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(route)).StatusCode);

            HttpRequestMessage asGuest = new(HttpMethod.Get, route);
            asGuest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guestToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(asGuest)).StatusCode);
        }
    }

    [Fact]
    public async Task StatsSummary_ReflectsRecordedActivity()
    {
        await _client.PostAsJsonAsync("api/v1/analytics/page-views", new PageViewRequest { Path = "/summary-check" });

        string adminToken = await LoginAsAdminAsync();
        HttpRequestMessage request = new(HttpMethod.Get, "api/v1/admin/stats/summary?days=7");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        StatsSummaryResponse summary = (await response.Content.ReadFromJsonAsync<StatsSummaryResponse>())!;
        Assert.True(summary.PageViews >= 1);
        Assert.True(summary.DailyVisitors >= 1);
    }

    [Fact]
    public async Task VerboseToggle_RoundTrips_AndIsAudited()
    {
        string adminToken = await LoginAsAdminAsync();

        HttpRequestMessage enable = new(HttpMethod.Put, "api/v1/admin/logs/verbose")
        {
            Content = JsonContent.Create(new VerboseRequest { Enabled = true })
        };
        enable.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        HttpResponseMessage enableResponse = await _client.SendAsync(enable);
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);
        VerboseStatusResponse status = (await enableResponse.Content.ReadFromJsonAsync<VerboseStatusResponse>())!;
        Assert.True(status.Enabled);
        Assert.NotNull(status.UntilUtc);

        HttpRequestMessage disable = new(HttpMethod.Put, "api/v1/admin/logs/verbose")
        {
            Content = JsonContent.Create(new VerboseRequest { Enabled = false })
        };
        disable.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        VerboseStatusResponse offStatus = (await (await _client.SendAsync(disable)).Content.ReadFromJsonAsync<VerboseStatusResponse>())!;
        Assert.False(offStatus.Enabled);

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();
        Assert.True(db.AuditEvents.Any(a => a.Action == AuditActions.VerboseEnabled && a.ActorName == "admin"));
        Assert.True(db.AuditEvents.Any(a => a.Action == AuditActions.VerboseDisabled && a.ActorName == "admin"));
    }

    [Fact]
    public async Task FailedLogin_WritesAuditEvent()
    {
        await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest { UserName = "admin", Password = "wrong-password-1" });

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();
        Assert.True(db.AuditEvents.Any(a => a.Action == AuditActions.LoginFailed && a.Target == "admin"));
    }

    // ---- Maintenance: rollup + retention -------------------------------------------

    [Fact]
    public async Task Maintenance_RollsUpCompletedDays_AndPurgesExpiredRows()
    {
        DateTime nowUtc = DateTime.UtcNow;
        DateOnly yesterday = DateOnly.FromDateTime(nowUtc).AddDays(-1);
        DateTime yesterdayNoon = yesterday.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

        using (IServiceScope seedScope = _factory.Services.CreateScope())
        {
            FairShareDbContext db = seedScope.ServiceProvider.GetRequiredService<FairShareDbContext>();
            db.PageViews.Add(new PageView { Path = "/", VisitorKey = "k1", OccurredAtUtc = yesterdayNoon });
            db.PageViews.Add(new PageView { Path = "/", VisitorKey = "k2", OccurredAtUtc = yesterdayNoon });
            db.AnalyticsEvents.Add(new AnalyticsEvent { Name = "gated-hit", Target = "profiles", VisitorKey = "k1", OccurredAtUtc = yesterdayNoon });
            db.Logs.Add(new LogEntry { OccurredAtUtc = nowUtc.AddDays(-40), Level = 2, Category = "FairShare.Old", Message = "expired" });
            db.Logs.Add(new LogEntry { OccurredAtUtc = nowUtc, Level = 2, Category = "FairShare.New", Message = "fresh" });
            db.AuditEvents.Add(new AuditEvent { OccurredAtUtc = nowUtc.AddDays(-400), Action = "login-succeeded", ActorName = "ancient" });
            await db.SaveChangesAsync();
        }

        ObservabilityMaintenanceService maintenance = new(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<ObservabilityMaintenanceService>.Instance);

        await maintenance.RunOnceAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext verify = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

        DailySiteStat site = verify.DailySiteStats.Single(s => s.Day == yesterday);
        Assert.Equal(2, site.Views);
        Assert.Equal(2, site.Visitors);
        Assert.Equal(1, verify.DailyEventStats.Count(e => e.Day == yesterday && e.Name == "gated-hit"));

        Assert.False(verify.Logs.Any(l => l.Category == "FairShare.Old"), "30-day log retention should have purged the old row");
        Assert.True(verify.Logs.Any(l => l.Category == "FairShare.New"));
        Assert.False(verify.AuditEvents.Any(a => a.ActorName == "ancient"), "365-day audit retention should have purged the old row");

        // Idempotent: a second run must not double the aggregates.
        await maintenance.RunOnceAsync();
        using IServiceScope secondScope = _factory.Services.CreateScope();
        FairShareDbContext verifyAgain = secondScope.ServiceProvider.GetRequiredService<FairShareDbContext>();
        Assert.Equal(2, verifyAgain.DailySiteStats.Single(s => s.Day == yesterday).Views);
    }

    // ---- Helpers -------------------------------------------------------------------

    private int CountPageViews()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<FairShareDbContext>().PageViews.Count();
    }

    private async Task<string> LoginAsAdminAsync()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Adm!n-Test-12345"
        });

        AuthTokenResponse tokens = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        return tokens.AccessToken;
    }

    private async Task<string> StartGuestAsync()
    {
        HttpResponseMessage response = await _client.PostAsync("api/v1/auth/guest", content: null);
        AuthTokenResponse tokens = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        return tokens.AccessToken;
    }
}

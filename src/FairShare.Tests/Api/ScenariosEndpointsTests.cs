using FairShare.Contracts.Auth;
using FairShare.Contracts.Calculation;
using FairShare.Contracts.Scenarios;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class ScenariosEndpointsTests : IClassFixture<FairShareApiFactory>
{
    private readonly HttpClient _client;

    public ScenariosEndpointsTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        _client.DefaultRequestHeaders.Add("X-FairShare-Auth", "1");
    }

    // The CS-42 workbook sample case: defendant pays $50.
    private static ScenarioSaveRequest AlabamaScenario(string name) => new()
    {
        Name = name,
        State = "AL",
        Form = "CS42",
        Inputs = new CalculationRequest
        {
            NumberOfChildren = 1,
            Plaintiff = new ParentDataDto { HasPrimaryCustody = true, MonthlyGrossIncome = 1200, HealthcareCoverageCosts = 100 },
            Defendant = new ParentDataDto { MonthlyGrossIncome = 1000, WorkRelatedChildcareCosts = 20 }
        }
    };

    // The Oregon golden "sole-custody-payer-premium" case: plaintiff pays $506.
    private static ScenarioSaveRequest OregonScenario(string name) => new()
    {
        Name = name,
        State = "OR",
        Form = "Worksheet",
        Inputs = new CalculationRequest
        {
            Oregon = new OregonCalculationRequest
            {
                Plaintiff = new OregonParentDto { MonthlyIncome = 4500, ChildrensHealthCoverageCost = 250, AverageOvernights = 91 },
                Defendant = new OregonParentDto { MonthlyIncome = 3200, AverageOvernights = 274 },
                JointMinorChildren = 2,
                CashMedical = "No",
                CoverageSelection = "Plaintiff"
            }
        }
    };

    [Fact]
    public async Task Scenarios_AsGuest_AreForbidden()
    {
        HttpResponseMessage guest = await _client.PostAsync("api/v1/auth/guest", content: null);
        AuthTokenResponse tokens = (await guest.Content.ReadFromJsonAsync<AuthTokenResponse>())!;

        HttpResponseMessage list = await SendAsync(HttpMethod.Get, "api/v1/scenarios", tokens.AccessToken, body: null);
        HttpResponseMessage save = await SendAsync(HttpMethod.Post, "api/v1/scenarios", tokens.AccessToken, AlabamaScenario("guest-try"));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);
    }

    [Fact]
    public async Task SaveListReopenDelete_RoundTrip()
    {
        string token = await LoginAsAdminAsync();

        // Save an Oregon scenario; the server computes the snapshot itself.
        HttpResponseMessage saved = await SendAsync(HttpMethod.Post, "api/v1/scenarios", token, OregonScenario("roundtrip-or"));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        ScenarioSummaryDto summary = (await saved.Content.ReadFromJsonAsync<ScenarioSummaryDto>())!;

        Assert.Equal("OR", summary.State);
        Assert.Equal("Plaintiff", summary.Payer);
        Assert.Equal(506, summary.FinalAmount);
        Assert.Equal("2026-07-01", summary.RuleEffectiveDate);

        // It lists.
        HttpResponseMessage listResponse = await SendAsync(HttpMethod.Get, "api/v1/scenarios", token, body: null);
        List<ScenarioSummaryDto> list = (await listResponse.Content.ReadFromJsonAsync<List<ScenarioSummaryDto>>())!;
        Assert.Contains(list, s => s.Id == summary.Id);

        // Reopening recomputes under current rules; nothing changed since save, so no notice.
        HttpResponseMessage reopened = await SendAsync(HttpMethod.Get, $"api/v1/scenarios/{summary.Id}", token, body: null);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        ScenarioDetailDto detail = (await reopened.Content.ReadFromJsonAsync<ScenarioDetailDto>())!;

        Assert.False(detail.ResultChanged);
        Assert.NotNull(detail.Current);
        Assert.Equal(506, detail.Snapshot.FinalAmount);
        Assert.Equal(506, detail.Current!.FinalAmount);
        Assert.Empty(detail.Snapshot.Lines);
        Assert.NotEmpty(detail.Current.Lines);
        Assert.Equal(2, detail.Inputs.Oregon!.JointMinorChildren);

        // Delete.
        HttpResponseMessage deleted = await SendAsync(HttpMethod.Delete, $"api/v1/scenarios/{summary.Id}", token, body: null);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        HttpResponseMessage gone = await SendAsync(HttpMethod.Get, $"api/v1/scenarios/{summary.Id}", token, body: null);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Save_SameName_UpdatesInPlace()
    {
        string token = await LoginAsAdminAsync();

        HttpResponseMessage first = await SendAsync(HttpMethod.Post, "api/v1/scenarios", token, AlabamaScenario("upsert-case"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        ScenarioSummaryDto created = (await first.Content.ReadFromJsonAsync<ScenarioSummaryDto>())!;

        // Same name (different case), different figures: updates the same record.
        ScenarioSaveRequest changed = AlabamaScenario("UPSERT-CASE");
        changed.Inputs.Defendant.MonthlyGrossIncome = 2000;

        HttpResponseMessage second = await SendAsync(HttpMethod.Post, "api/v1/scenarios", token, changed);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        ScenarioSummaryDto updated = (await second.Content.ReadFromJsonAsync<ScenarioSummaryDto>())!;

        Assert.Equal(created.Id, updated.Id);
        Assert.NotEqual(created.FinalAmount, updated.FinalAmount);

        await SendAsync(HttpMethod.Delete, $"api/v1/scenarios/{created.Id}", token, body: null);
    }

    [Fact]
    public async Task Save_FailingInputs_IsRejected()
    {
        string token = await LoginAsAdminAsync();

        // Overnights not totaling 365 - the worksheet rejects it, so there is no number to preserve.
        ScenarioSaveRequest bad = OregonScenario("bad-overnights");
        bad.Inputs.Oregon!.Plaintiff.AverageOvernights = 10;

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, "api/v1/scenarios", token, bad);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Save_UnknownForm_IsNotFound()
    {
        string token = await LoginAsAdminAsync();

        ScenarioSaveRequest bad = AlabamaScenario("bogus-form");
        bad.Form = "BOGUS";

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, "api/v1/scenarios", token, bad);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void ResultsDiffer_FlagsHeadlineAndOregonTotals()
    {
        CalculationResponse baseline = new() { Payer = "Plaintiff", FinalAmount = 506 };

        Assert.False(FairShare.Api.Controllers.ScenariosController.ResultsDiffer(baseline, new() { Payer = "Plaintiff", FinalAmount = 506 }));
        Assert.True(FairShare.Api.Controllers.ScenariosController.ResultsDiffer(baseline, new() { Payer = "Plaintiff", FinalAmount = 512 }));
        Assert.True(FairShare.Api.Controllers.ScenariosController.ResultsDiffer(baseline, new() { Payer = "Defendant", FinalAmount = 506 }));
        Assert.True(FairShare.Api.Controllers.ScenariosController.ResultsDiffer(
            new() { Payer = "", FinalAmount = 0, Oregon = new OregonResultDto { PlaintiffTotalSupport = 100, DefendantTotalSupport = 200 } },
            new() { Payer = "", FinalAmount = 0, Oregon = new OregonResultDto { PlaintiffTotalSupport = 100, DefendantTotalSupport = 250 } }));
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

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string accessToken, object? body)
    {
        using HttpRequestMessage request = new(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        return await _client.SendAsync(request);
    }
}

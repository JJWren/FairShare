using FairShare.Contracts.Auth;
using FairShare.Contracts.Calculation;
using FairShare.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class OregonCalculationsEndpointsTests : IClassFixture<FairShareApiFactory>
{
    private readonly HttpClient _client;

    public OregonCalculationsEndpointsTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    // The "sole-custody-payer-premium" golden case (or-worksheet-golden.json): values read back
    // from the official DOJ workbook - plaintiff pays $506 total for the two minors.
    private static CalculationRequest GoldenCaseRequest() => new()
    {
        Oregon = new OregonCalculationRequest
        {
            Plaintiff = new OregonParentDto { MonthlyIncome = 4500, ChildrensHealthCoverageCost = 250, AverageOvernights = 91 },
            Defendant = new OregonParentDto { MonthlyIncome = 3200, AverageOvernights = 274 },
            JointMinorChildren = 2,
            CashMedical = "No",
            CoverageSelection = "Plaintiff"
        }
    };

    [Fact]
    public async Task PostCalculation_OregonWorksheet_ReturnsLinesPayerAndExtras()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/OR/forms/Worksheet/calculations", accessToken, GoldenCaseRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CalculationResponse body = (await response.Content.ReadFromJsonAsync<CalculationResponse>())!;

        Assert.True(body.Success);
        Assert.Equal("OR", body.State);
        Assert.Equal("Worksheet", body.Form);
        Assert.Equal(2, body.NumberOfChildren);
        Assert.Equal("Plaintiff", body.Payer);
        Assert.Equal(506, body.FinalAmount);
        // Every estimate names the rule data it implements (#171).
        Assert.Equal("OAR 137-050 effective 2026-07-01", body.RuleVintage);

        Assert.NotNull(body.Oregon);
        Assert.Equal(506, body.Oregon!.PlaintiffTotalSupport);
        Assert.Equal(0, body.Oregon.DefendantTotalSupport);
        Assert.Equal("Plaintiff", body.Oregon.PaysForMinorChildren);
        Assert.Equal("Plaintiff", body.Oregon.CoverageProvider);
        Assert.Equal("2026-07-01", body.Oregon.RuleEffectiveDate);

        Assert.Equal("Percent", body.Lines.Single(l => l.Number == "1i").Format);
        Assert.Equal("Number", body.Lines.Single(l => l.Number == "1d").Format);
        Assert.Equal(506, body.Lines.Single(l => l.Number == "9e").Plaintiff);
    }

    [Fact]
    public async Task PostCalculation_Oregon_WithoutOregonInputs_ReturnsBadRequest()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/OR/forms/Worksheet/calculations", accessToken, new CalculationRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCalculation_Oregon_BadCashMedicalValue_ReturnsBadRequest()
    {
        string accessToken = await LoginAsAdminAsync();

        CalculationRequest request = GoldenCaseRequest();
        request.Oregon!.CashMedical = "maybe";

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/OR/forms/Worksheet/calculations", accessToken, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetStates_ListsOregon()
    {
        HttpResponseMessage response = await _client.GetAsync("api/v1/states");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<StateSummaryDto> states = (await response.Content.ReadFromJsonAsync<List<StateSummaryDto>>())!;

        Assert.Contains(states, s => s.State == "OR");
    }

    [Fact]
    public async Task GetForms_Oregon_ListsTheWorksheet()
    {
        HttpResponseMessage response = await _client.GetAsync("api/v1/states/OR/forms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<FormSummaryDto> forms = (await response.Content.ReadFromJsonAsync<List<FormSummaryDto>>())!;

        FormSummaryDto worksheet = Assert.Single(forms);
        Assert.Equal("Worksheet", worksheet.Form);
        Assert.Equal("Child Support Worksheet (CSF 02 0910)", worksheet.DisplayName);
        Assert.False(worksheet.IsSharedCustody);
    }

    [Fact]
    public async Task ExportXlsx_Oregon_ReturnsNotFound()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/OR/forms/Worksheet/calculations/export/xlsx", accessToken, GoldenCaseRequest());

        // No Oregon export template yet - the endpoint must say so rather than produce a wrong file.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpMethod method, string url, string accessToken, object body)
    {
        using HttpRequestMessage request = new(method, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        return await _client.SendAsync(request);
    }
}

using FairShare.Contracts.Auth;
using FairShare.Contracts.Calculation;
using FairShare.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class CalculationsEndpointsTests : IClassFixture<FairShareApiFactory>
{
    private readonly HttpClient _client;

    public CalculationsEndpointsTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    // The official workbook's own sample figures: 1200/1000, 1 child, plaintiff pays $100 health care,
    // defendant $20 child care -> CS-42 line 13 = 186 / 50.
    [Fact]
    public async Task PostCalculation_CS42_ReturnsWorksheetLines()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/AL/forms/CS42/calculations", accessToken, WorkbookDefaults(plaintiffPrimary: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CalculationResponse body = (await response.Content.ReadFromJsonAsync<CalculationResponse>())!;

        Assert.True(body.Success);
        Assert.Equal("Defendant", body.Payer);
        Assert.Equal(50, body.FinalAmount);
        Assert.Equal(15, body.Lines.Count);
        Assert.Equal("Percent", body.Lines.Single(l => l.Number == "3").Format);

        WorksheetLineDto order = body.Lines.Single(l => l.Number == "13");
        Assert.Equal(186, order.Plaintiff);
        Assert.Equal(50, order.Defendant);
        Assert.Null(order.Combined);
    }

    [Fact]
    public async Task PostCalculation_CS42S_ReturnsWorksheetLines()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/AL/forms/CS42S/calculations", accessToken, WorkbookDefaults(plaintiffPrimary: false));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CalculationResponse body = (await response.Content.ReadFromJsonAsync<CalculationResponse>())!;

        Assert.True(body.Success);
        Assert.Equal("Defendant", body.Payer);
        Assert.Equal(2, body.FinalAmount);
        Assert.Equal(16, body.Lines.Count);

        WorksheetLineDto order = body.Lines.Single(l => l.Number == "14");
        Assert.Null(order.Plaintiff);
        Assert.Equal(2, order.Defendant);
    }

    [Fact]
    public async Task PostCalculation_IncomeAboveSchedule_ReturnsErrorInBody()
    {
        string accessToken = await LoginAsAdminAsync();

        CalculationRequest request = new()
        {
            NumberOfChildren = 2,
            Plaintiff = new ParentDataDto { HasPrimaryCustody = true, MonthlyGrossIncome = 20025 },
            Defendant = new ParentDataDto { MonthlyGrossIncome = 10000 }
        };

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/AL/forms/CS42/calculations", accessToken, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CalculationResponse body = (await response.Content.ReadFromJsonAsync<CalculationResponse>())!;
        Assert.False(body.Success);
        Assert.Contains(body.Errors, e => e.Code == "INCOME_ABOVE_SCHEDULE");
        Assert.Empty(body.Lines);
    }

    [Fact]
    public async Task PostCalculation_UnknownForm_ReturnsNotFound()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post,
            "api/v1/states/AL/forms/BOGUS/calculations", accessToken, WorkbookDefaults(plaintiffPrimary: true));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetForms_ReturnsDisplayNamesAndDescriptions()
    {
        string accessToken = await LoginAsAdminAsync();

        using HttpRequestMessage request = new(HttpMethod.Get, "api/v1/states/AL/forms");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<FormSummaryDto> forms = (await response.Content.ReadFromJsonAsync<List<FormSummaryDto>>())!;

        Assert.Equal(["CS42", "CS42S"], forms.Select(f => f.Form));

        FormSummaryDto cs42 = forms.Single(f => f.Form == "CS42");
        Assert.Equal("CS-42 (Rev. 5/2022)", cs42.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(cs42.Description));
        Assert.False(cs42.IsSharedCustody);

        FormSummaryDto cs42s = forms.Single(f => f.Form == "CS42S");
        Assert.Equal("CS-42-S (Eff. 6/2023)", cs42s.DisplayName);
        Assert.True(cs42s.IsSharedCustody);
    }

    private static CalculationRequest WorkbookDefaults(bool plaintiffPrimary) => new()
    {
        NumberOfChildren = 1,
        Plaintiff = new ParentDataDto { HasPrimaryCustody = plaintiffPrimary, MonthlyGrossIncome = 1200, HealthcareCoverageCosts = 100 },
        Defendant = new ParentDataDto { MonthlyGrossIncome = 1000, WorkRelatedChildcareCosts = 20 }
    };

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

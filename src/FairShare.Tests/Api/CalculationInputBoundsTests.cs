using FairShare.Contracts.Auth;
using FairShare.Contracts.Calculation;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

/// <summary>
/// Money inputs are bounded at the API surface (#153): absurd values fail validation with
/// a 400 instead of reaching arithmetic that can overflow (Oregon) or wrap into a wrong
/// schedule row (Alabama).
/// </summary>
[Collection("Api")]
public class CalculationInputBoundsTests : IClassFixture<FairShareApiFactory>
{
    private readonly HttpClient _client;

    public CalculationInputBoundsTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Alabama_MoneyAboveCap_FailsValidation()
    {
        string token = await LoginAsAdminAsync();

        CalculationRequest request = new()
        {
            NumberOfChildren = 1,
            Plaintiff = new ParentDataDto { HasPrimaryCustody = true, MonthlyGrossIncome = 999_999_999 },
            Defendant = new ParentDataDto { MonthlyGrossIncome = 1000 }
        };

        HttpResponseMessage response = await SendAsync("api/v1/states/AL/forms/CS42/calculations", token, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Oregon_MoneyAboveCap_FailsValidation()
    {
        string token = await LoginAsAdminAsync();

        CalculationRequest request = new()
        {
            Oregon = new OregonCalculationRequest
            {
                Plaintiff = new OregonParentDto { MonthlyIncome = 99_999_999_999m, AverageOvernights = 91 },
                Defendant = new OregonParentDto { MonthlyIncome = 3200, AverageOvernights = 274 },
                JointMinorChildren = 1
            }
        };

        HttpResponseMessage response = await SendAsync("api/v1/states/OR/forms/Worksheet/calculations", token, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Alabama_RealisticMoney_StillCalculates()
    {
        string token = await LoginAsAdminAsync();

        CalculationRequest request = new()
        {
            NumberOfChildren = 1,
            Plaintiff = new ParentDataDto { HasPrimaryCustody = true, MonthlyGrossIncome = 1200, HealthcareCoverageCosts = 100 },
            Defendant = new ParentDataDto { MonthlyGrossIncome = 1000, WorkRelatedChildcareCosts = 20 }
        };

        HttpResponseMessage response = await SendAsync("api/v1/states/AL/forms/CS42/calculations", token, request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CalculationResponse body = (await response.Content.ReadFromJsonAsync<CalculationResponse>())!;
        Assert.True(body.Success);
    }

    private async Task<string> LoginAsAdminAsync()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Adm!n-Test-12345"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AuthTokenResponse tokens = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        return tokens.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAsync(string url, string accessToken, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        return await _client.SendAsync(request);
    }
}

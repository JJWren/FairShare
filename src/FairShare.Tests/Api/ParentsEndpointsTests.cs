using FairShare.Contracts.Auth;
using FairShare.Contracts.Parents;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class ParentsEndpointsTests : IClassFixture<FairShareApiFactory>
{
    private readonly HttpClient _client;

    public ParentsEndpointsTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task UpdateParent_WithStaleRowVersion_ReturnsConflict()
    {
        string accessToken = await LoginAsAdminAsync();

        ParentProfileDto created = await CreateParentAsync(accessToken, "RowVersion Conflict Test");

        ParentProfileUpdateRequest update = ToUpdateRequest(created, displayName: "Renamed");
        update.RowVersion = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]);

        HttpResponseMessage response = await SendAuthorizedAsync(
            HttpMethod.Put, $"api/v1/parents/{created.Id}", accessToken, update);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateParent_WithoutRowVersion_Succeeds()
    {
        string accessToken = await LoginAsAdminAsync();

        ParentProfileDto created = await CreateParentAsync(accessToken, "RowVersion Absent Test");

        ParentProfileUpdateRequest update = ToUpdateRequest(created, displayName: "Renamed Freely");

        HttpResponseMessage response = await SendAuthorizedAsync(
            HttpMethod.Put, $"api/v1/parents/{created.Id}", accessToken, update);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateParent_WithMalformedRowVersion_ReturnsBadRequest()
    {
        string accessToken = await LoginAsAdminAsync();

        ParentProfileDto created = await CreateParentAsync(accessToken, "RowVersion Malformed Test");

        ParentProfileUpdateRequest update = ToUpdateRequest(created, displayName: "Renamed");
        update.RowVersion = "not-base64!!!";

        HttpResponseMessage response = await SendAuthorizedAsync(
            HttpMethod.Put, $"api/v1/parents/{created.Id}", accessToken, update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private async Task<ParentProfileDto> CreateParentAsync(string accessToken, string displayName)
    {
        HttpResponseMessage response = await SendAuthorizedAsync(HttpMethod.Post, "api/v1/parents", accessToken,
            new ParentProfileCreateRequest
            {
                DisplayName = displayName,
                MonthlyGrossIncome = 4000,
                Deduplicate = false
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ParentProfileDto>())!;
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

    private static ParentProfileUpdateRequest ToUpdateRequest(ParentProfileDto dto, string displayName) => new()
    {
        DisplayName = displayName,
        MonthlyGrossIncome = dto.MonthlyGrossIncome,
        PreexistingChildSupport = dto.PreexistingChildSupport,
        PreexistingAlimony = dto.PreexistingAlimony,
        WorkRelatedChildcareCosts = dto.WorkRelatedChildcareCosts,
        HealthcareCoverageCosts = dto.HealthcareCoverageCosts,
        HasPrimaryCustody = dto.HasPrimaryCustody
    };
}

using FairShare.Api.Models;
using FairShare.Api.Persistence;
using FairShare.Contracts.Admin;
using FairShare.Contracts.Auth;
using FairShare.Contracts.Calculation;
using FairShare.Contracts.Parents;
using FairShare.Contracts.Scenarios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FairShare.Tests.Api;

/// <summary>
/// The product's core privacy invariant (#146/#147): one account can never read, list,
/// change, or delete another account's saved data - and "one account" includes admins,
/// because parent profiles and scenarios are strictly owner-scoped with no admin
/// cross-access. Owner mismatches read as 404, never 403, so existence is not disclosed.
/// </summary>
[Collection("Api")]
public class OwnershipIsolationTests : IClassFixture<FairShareApiFactory>
{
    private readonly FairShareApiFactory _factory;
    private readonly HttpClient _client;

    public OwnershipIsolationTests(FairShareApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Admin_CannotReadAnotherUsersParent()
    {
        string adminToken = await LoginAsAdminAsync();
        string ownerToken = await CreateAndLoginUserAsync("isolation-read-owner");
        ParentProfileDto owned = await CreateParentAsync(ownerToken, "Isolation Read Target");

        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"api/v1/parents/{owned.Id}", adminToken, body: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotUpdateAnotherUsersParent()
    {
        string adminToken = await LoginAsAdminAsync();
        string ownerToken = await CreateAndLoginUserAsync("isolation-update-owner");
        ParentProfileDto owned = await CreateParentAsync(ownerToken, "Isolation Update Target");

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Put, $"api/v1/parents/{owned.Id}", adminToken, ToUpdateRequest(owned, displayName: "Hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The owner's record is untouched.
        HttpResponseMessage ownerRead = await SendAsync(HttpMethod.Get, $"api/v1/parents/{owned.Id}", ownerToken, body: null);
        Assert.Equal(HttpStatusCode.OK, ownerRead.StatusCode);
        ParentProfileDto after = (await ownerRead.Content.ReadFromJsonAsync<ParentProfileDto>())!;
        Assert.Equal("Isolation Update Target", after.DisplayName);
    }

    [Fact]
    public async Task Admin_CannotArchiveAnotherUsersParent()
    {
        string adminToken = await LoginAsAdminAsync();
        string ownerToken = await CreateAndLoginUserAsync("isolation-archive-owner");
        ParentProfileDto owned = await CreateParentAsync(ownerToken, "Isolation Archive Target");

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, $"api/v1/parents/{owned.Id}/archive", adminToken, body: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        HttpResponseMessage ownerRead = await SendAsync(HttpMethod.Get, $"api/v1/parents/{owned.Id}", ownerToken, body: null);
        Assert.Equal(HttpStatusCode.OK, ownerRead.StatusCode);
    }

    [Fact]
    public async Task Admin_List_ContainsOnlyTheAdminsOwnParents()
    {
        string adminToken = await LoginAsAdminAsync();
        string ownerToken = await CreateAndLoginUserAsync("isolation-list-owner");
        await CreateParentAsync(ownerToken, "Isolation List Foreign");
        ParentProfileDto own = await CreateParentAsync(adminToken, "Isolation List Own");

        HttpResponseMessage response = await SendAsync(HttpMethod.Get, "api/v1/parents", adminToken, body: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<ParentProfileDto> list = (await response.Content.ReadFromJsonAsync<List<ParentProfileDto>>())!;

        Assert.Contains(list, p => p.Id == own.Id);
        Assert.DoesNotContain(list, p => p.DisplayName == "Isolation List Foreign");
    }

    [Fact]
    public async Task User_CannotReadAnotherUsersParent()
    {
        string ownerToken = await CreateAndLoginUserAsync("isolation-user-owner");
        string otherToken = await CreateAndLoginUserAsync("isolation-user-other");
        ParentProfileDto owned = await CreateParentAsync(ownerToken, "Isolation Cross User Parent");

        HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"api/v1/parents/{owned.Id}", otherToken, body: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_OwnProfileSurvivesBeyondTheGlobalPageSize()
    {
        string ownerToken = await CreateAndLoginUserAsync("isolation-page-owner");
        ParentProfileDto own = await CreateParentAsync(ownerToken, "ZZZ Page Survivor");

        // 101 alphabetically-earlier rows owned by nobody (the legacy ownerless shape).
        // If the page limit were applied before ownership, these would crowd the caller's
        // own row out of the name-ordered page entirely.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

            for (int i = 0; i < 101; i++)
            {
                db.ParentProfiles.Add(new ParentProfile
                {
                    Id = Guid.NewGuid(),
                    DisplayName = $"AAA Crowd {i:D3}",
                    MonthlyGrossIncome = 1000
                });
            }

            await db.SaveChangesAsync();
        }

        HttpResponseMessage response = await SendAsync(HttpMethod.Get, "api/v1/parents", ownerToken, body: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<ParentProfileDto> list = (await response.Content.ReadFromJsonAsync<List<ParentProfileDto>>())!;

        Assert.Contains(list, p => p.Id == own.Id);
        // Ownerless legacy rows belong to nobody and must never be listed to anybody.
        Assert.DoesNotContain(list, p => p.DisplayName.StartsWith("AAA Crowd"));
    }

    [Fact]
    public async Task User_CannotReadOrDeleteAnotherUsersScenario()
    {
        string ownerToken = await CreateAndLoginUserAsync("isolation-scenario-owner");
        string otherToken = await CreateAndLoginUserAsync("isolation-scenario-other");

        HttpResponseMessage saved = await SendAsync(HttpMethod.Post, "api/v1/scenarios", ownerToken, AlabamaScenario("isolation-scenario"));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        ScenarioSummaryDto summary = (await saved.Content.ReadFromJsonAsync<ScenarioSummaryDto>())!;

        HttpResponseMessage read = await SendAsync(HttpMethod.Get, $"api/v1/scenarios/{summary.Id}", otherToken, body: null);
        HttpResponseMessage delete = await SendAsync(HttpMethod.Delete, $"api/v1/scenarios/{summary.Id}", otherToken, body: null);
        HttpResponseMessage ownerStillHasIt = await SendAsync(HttpMethod.Get, $"api/v1/scenarios/{summary.Id}", ownerToken, body: null);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerStillHasIt.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotReadAnotherUsersScenario()
    {
        string adminToken = await LoginAsAdminAsync();
        string ownerToken = await CreateAndLoginUserAsync("isolation-scenario-vs-admin");

        HttpResponseMessage saved = await SendAsync(HttpMethod.Post, "api/v1/scenarios", ownerToken, AlabamaScenario("isolation-scenario-admin"));
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        ScenarioSummaryDto summary = (await saved.Content.ReadFromJsonAsync<ScenarioSummaryDto>())!;

        HttpResponseMessage read = await SendAsync(HttpMethod.Get, $"api/v1/scenarios/{summary.Id}", adminToken, body: null);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    // The CS-42 workbook sample case, as in ScenariosEndpointsTests.
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

    // Self-registration is disabled, so second users are provisioned the way an operator
    // would: created by the admin, then logged in.
    private async Task<string> CreateAndLoginUserAsync(string userName)
    {
        string adminToken = await LoginAsAdminAsync();

        HttpResponseMessage createResponse = await SendAsync(HttpMethod.Post, "api/v1/admin/users", adminToken,
            new CreateUserRequest
            {
                UserName = userName,
                Password = "Isolation-Test-12345!",
                ConfirmPassword = "Isolation-Test-12345!",
                Role = "User"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        HttpResponseMessage loginResponse = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = "Isolation-Test-12345!"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        AuthTokenResponse tokens = (await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        return tokens.AccessToken;
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
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        return tokens.AccessToken;
    }

    private async Task<ParentProfileDto> CreateParentAsync(string accessToken, string displayName)
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, "api/v1/parents", accessToken,
            new ParentProfileCreateRequest
            {
                DisplayName = displayName,
                MonthlyGrossIncome = 4000
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ParentProfileDto>())!;
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

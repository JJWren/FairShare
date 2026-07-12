using FairShare.Contracts.Admin;
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

    [Fact]
    public async Task CreateParent_SameNameChangedFields_UpdatesExistingRecordInPlace()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage first = await SendAuthorizedAsync(HttpMethod.Post, "api/v1/parents", accessToken,
            new ParentProfileCreateRequest { DisplayName = "Upsert Test", MonthlyGrossIncome = 4244 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        ParentProfileDto created = (await first.Content.ReadFromJsonAsync<ParentProfileDto>())!;

        // Same name, different figures - must modify the existing record, not add a twin.
        HttpResponseMessage second = await SendAuthorizedAsync(HttpMethod.Post, "api/v1/parents", accessToken,
            new ParentProfileCreateRequest { DisplayName = "Upsert Test", MonthlyGrossIncome = 5000, HealthcareCoverageCosts = 195 });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        ParentProfileDto updated = (await second.Content.ReadFromJsonAsync<ParentProfileDto>())!;

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(5000, updated.MonthlyGrossIncome);
        Assert.Equal(195, updated.HealthcareCoverageCosts);

        HttpResponseMessage list = await SendAuthorizedGetAsync("api/v1/parents?q=Upsert Test", accessToken);
        List<ParentProfileDto> matches = (await list.Content.ReadFromJsonAsync<List<ParentProfileDto>>())!;
        Assert.Single(matches, p => p.DisplayName == "Upsert Test");
    }

    [Fact]
    public async Task CreateParent_SameNameDifferentCase_UpdatesExistingRecord()
    {
        string accessToken = await LoginAsAdminAsync();

        HttpResponseMessage first = await SendAuthorizedAsync(HttpMethod.Post, "api/v1/parents", accessToken,
            new ParentProfileCreateRequest { DisplayName = "Case Test", MonthlyGrossIncome = 3000 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        ParentProfileDto created = (await first.Content.ReadFromJsonAsync<ParentProfileDto>())!;

        HttpResponseMessage second = await SendAuthorizedAsync(HttpMethod.Post, "api/v1/parents", accessToken,
            new ParentProfileCreateRequest { DisplayName = "case test", MonthlyGrossIncome = 3500 });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        ParentProfileDto updated = (await second.Content.ReadFromJsonAsync<ParentProfileDto>())!;

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(3500, updated.MonthlyGrossIncome);
    }

    [Fact]
    public async Task CreateParent_SameName_DifferentUsers_KeepSeparateRecords()
    {
        string adminToken = await LoginAsAdminAsync();
        string otherToken = await RegisterUserAsync("upsert-other-user");

        HttpResponseMessage adminCreate = await SendAuthorizedAsync(HttpMethod.Post, "api/v1/parents", adminToken,
            new ParentProfileCreateRequest { DisplayName = "Shared Name", MonthlyGrossIncome = 4000 });
        Assert.Equal(HttpStatusCode.Created, adminCreate.StatusCode);
        ParentProfileDto adminProfile = (await adminCreate.Content.ReadFromJsonAsync<ParentProfileDto>())!;

        // The other user saving the same name must get their OWN record, not touch admin's.
        HttpResponseMessage otherCreate = await SendAuthorizedAsync(HttpMethod.Post, "api/v1/parents", otherToken,
            new ParentProfileCreateRequest { DisplayName = "Shared Name", MonthlyGrossIncome = 9999 });
        Assert.Equal(HttpStatusCode.Created, otherCreate.StatusCode);
        ParentProfileDto otherProfile = (await otherCreate.Content.ReadFromJsonAsync<ParentProfileDto>())!;

        Assert.NotEqual(adminProfile.Id, otherProfile.Id);

        HttpResponseMessage adminGet = await SendAuthorizedGetAsync($"api/v1/parents/{adminProfile.Id}", adminToken);
        ParentProfileDto adminAfter = (await adminGet.Content.ReadFromJsonAsync<ParentProfileDto>())!;
        Assert.Equal(4000, adminAfter.MonthlyGrossIncome);
    }

    [Fact]
    public async Task UpdateParent_RenamingOntoExistingName_ReturnsConflict()
    {
        string accessToken = await LoginAsAdminAsync();

        ParentProfileDto keep = await CreateParentAsync(accessToken, "Rename Target");
        ParentProfileDto toRename = await CreateParentAsync(accessToken, "Rename Source");

        // The unique (owner, name) index must reject renaming onto a name already in use.
        HttpResponseMessage response = await SendAuthorizedAsync(
            HttpMethod.Put, $"api/v1/parents/{toRename.Id}", accessToken, ToUpdateRequest(toRename, displayName: "Rename Target"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotEqual(keep.Id, toRename.Id);
    }

    // Self-registration is disabled by default, so second users are provisioned the way
    // an operator would: created by the admin, then logged in.
    private async Task<string> RegisterUserAsync(string userName)
    {
        string adminToken = await LoginAsAdminAsync();

        HttpResponseMessage createResponse = await SendAuthorizedAsync(HttpMethod.Post, "api/v1/admin/users", adminToken,
            new CreateUserRequest
            {
                UserName = userName,
                Password = "Upsert-Test-12345!",
                ConfirmPassword = "Upsert-Test-12345!",
                Role = "User"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        HttpResponseMessage loginResponse = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = "Upsert-Test-12345!"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        AuthTokenResponse? tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        return tokens.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAuthorizedGetAsync(string url, string accessToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        return await _client.SendAsync(request);
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
                MonthlyGrossIncome = 4000
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

using System.Net.Http.Headers;
using System.Security.Cryptography;
using FairShare.Api.Observability;
using FairShare.Api.Persistence;
using FairShare.Contracts.Admin;
using FairShare.Contracts.Auth;
using FairShare.Contracts.Calculation;
using FairShare.Contracts.Parents;
using FairShare.Contracts.Scenarios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FairShare.Tests.Api;

[Collection("Api")]
public class AccountsEndpointsTests : IClassFixture<FairShareApiFactory>
{
    private readonly FairShareApiFactory _factory;
    private readonly HttpClient _client;

    public AccountsEndpointsTests(FairShareApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        _client.DefaultRequestHeaders.Add("X-FairShare-Auth", "1");
    }

    // ---- Google gating --------------------------------------------------------------

    [Fact]
    public async Task Config_WithoutGoogleConfigured_ReportsDisabled()
    {
        AuthConfigResponse? config = await _client.GetFromJsonAsync<AuthConfigResponse>("api/v1/auth/config");

        Assert.NotNull(config);
        Assert.False(config!.GoogleEnabled);
    }

    [Fact]
    public async Task GoogleStart_WithoutGoogleConfigured_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync("api/v1/auth/google/start?returnUrl=%2F");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_IsRetired()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/register", new { userName = "x", password = "Password-1" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Remember this device -------------------------------------------------------

    [Fact]
    public async Task Login_WithoutRemember_IssuesSessionCookie()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Adm!n-Test-12345",
            RememberDevice = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string cookie = GetRefreshCookie(response);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithRemember_IssuesPersistentCookie()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Adm!n-Test-12345",
            RememberDevice = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string cookie = GetRefreshCookie(response);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    // ---- TOTP (admin local account) --------------------------------------------------

    [Fact]
    public async Task TwoFactor_EnableRequireAtLoginThenDisable_FullRoundTrip()
    {
        string adminToken = await LoginAsAdminAsync();

        TwoFactorStatusResponse status = (await GetAsAdminAsync<TwoFactorStatusResponse>("api/v1/auth/2fa/status", adminToken))!;
        Assert.False(status.Enabled);

        TwoFactorSetupResponse setup = (await GetAsAdminAsync<TwoFactorSetupResponse>("api/v1/auth/2fa/setup", adminToken))!;
        Assert.False(string.IsNullOrWhiteSpace(setup.SharedKey));
        string secret = ExtractSecret(setup.AuthenticatorUri);

        HttpResponseMessage enable = await PostAsAdminAsync("api/v1/auth/2fa/enable", adminToken, new TwoFactorCodeRequest { Code = ComputeTotp(secret) });
        Assert.Equal(HttpStatusCode.NoContent, enable.StatusCode);

        // Password alone is no longer sufficient - and the challenge only appears AFTER
        // the password verified.
        HttpResponseMessage noCode = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Adm!n-Test-12345"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, noCode.StatusCode);
        TwoFactorRequiredResponse? challenge = await noCode.Content.ReadFromJsonAsync<TwoFactorRequiredResponse>();
        Assert.True(challenge!.RequiresTwoFactor);

        HttpResponseMessage wrongPassword = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "wrong-password-x",
            TwoFactorCode = ComputeTotp(secret)
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        // A bad password must never reveal that the account has a 2FA challenge (the body
        // here is the framework's generic problem details, not the challenge shape).
        TwoFactorRequiredResponse? wrongBody = null;
        try
        {
            wrongBody = await wrongPassword.Content.ReadFromJsonAsync<TwoFactorRequiredResponse>();
        }
        catch (Exception)
        {
            // Non-JSON/empty body is equally fine - the point is no challenge leaked.
        }
        Assert.NotEqual(true, wrongBody?.RequiresTwoFactor);

        HttpResponseMessage withCode = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Adm!n-Test-12345",
            TwoFactorCode = ComputeTotp(secret)
        });
        Assert.Equal(HttpStatusCode.OK, withCode.StatusCode);

        HttpResponseMessage disable = await PostAsAdminAsync("api/v1/auth/2fa/disable", adminToken, new TwoFactorCodeRequest { Code = ComputeTotp(secret) });
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();
        Assert.True(db.AuditEvents.Any(a => a.Action == AuditActions.TwoFactorEnabled));
        Assert.True(db.AuditEvents.Any(a => a.Action == AuditActions.TwoFactorDisabled));
    }

    [Fact]
    public async Task TwoFactorEndpoints_RejectNonAdmins()
    {
        string userToken = await CreateAndLoginUserAsync("totp-plain-user");

        HttpRequestMessage request = new(HttpMethod.Get, "api/v1/auth/2fa/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request)).StatusCode);
    }

    // ---- Display name ---------------------------------------------------------------

    [Fact]
    public async Task ChangeUserName_UpdatesNameAndAudits()
    {
        string token = await CreateAndLoginUserAsync("rename-me");

        HttpRequestMessage request = new(HttpMethod.Post, "api/v1/auth/account/username")
        {
            Content = JsonContent.Create(new ChangeUserNameRequest { NewUserName = "renamed-user" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AuthTokenResponse tokens = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        Assert.Equal("renamed-user", tokens.UserName);

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();
        Assert.True(db.AuditEvents.Any(a => a.Action == AuditActions.UserNameChanged && a.Target == "renamed-user"));
    }

    // ---- Hard delete ----------------------------------------------------------------

    [Fact]
    public async Task DeleteAccount_RemovesEverythingItOwns_ButAuditRowsRemain()
    {
        string token = await CreateAndLoginUserAsync("deleting-user");

        // Give the account something to own.
        HttpRequestMessage createProfile = new(HttpMethod.Post, "api/v1/parents")
        {
            Content = JsonContent.Create(new ParentProfileCreateRequest
            {
                DisplayName = "Ephemeral Parent",
                MonthlyGrossIncome = 1000
            })
        };
        createProfile.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage profileResponse = await _client.SendAsync(createProfile);
        Assert.True(profileResponse.IsSuccessStatusCode);

        // ... and a saved scenario, which stores the full worksheet inputs.
        HttpRequestMessage createScenario = new(HttpMethod.Post, "api/v1/scenarios")
        {
            Content = JsonContent.Create(EphemeralScenario("ephemeral-scenario"))
        };
        createScenario.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage scenarioResponse = await _client.SendAsync(createScenario);
        Assert.Equal(HttpStatusCode.Created, scenarioResponse.StatusCode);

        Guid userId;
        using (IServiceScope preScope = _factory.Services.CreateScope())
        {
            FairShareDbContext preDb = preScope.ServiceProvider.GetRequiredService<FairShareDbContext>();
            userId = preDb.Users.Single(u => u.UserName == "deleting-user").Id;
            Assert.True(preDb.RefreshTokens.Any(t => t.UserId == userId), "login should have minted a refresh token");
        }

        HttpRequestMessage deleteRequest = new(HttpMethod.Delete, "api/v1/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest { Confirm = "DELETE" })
        };
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // The credentials are gone for good.
        HttpResponseMessage loginAgain = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "deleting-user",
            Password = UserPassword
        });
        Assert.Equal(HttpStatusCode.Unauthorized, loginAgain.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

        Assert.False(db.Users.Any(u => u.UserName == "deleting-user"));
        Assert.False(db.ParentProfiles.Any(p => p.DisplayName == "Ephemeral Parent"));
        Assert.False(db.Scenarios.Any(s => s.OwnerUserId == userId));
        Assert.False(db.RefreshTokens.Any(t => t.UserId == userId));
        // The accountability record outlives the account (ADR 0004), until its own retention.
        Assert.True(db.AuditEvents.Any(a => a.Action == AuditActions.AccountDeleted && a.Target == "deleting-user"));
    }

    [Fact]
    public async Task AdminDeleteUser_RemovesOwnedRows_NeverOrphansThem()
    {
        string token = await CreateAndLoginUserAsync("admin-deleted-user");

        HttpRequestMessage createProfile = new(HttpMethod.Post, "api/v1/parents")
        {
            Content = JsonContent.Create(new ParentProfileCreateRequest
            {
                DisplayName = "Admin Delete Parent",
                MonthlyGrossIncome = 2000
            })
        };
        createProfile.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.True((await _client.SendAsync(createProfile)).IsSuccessStatusCode);

        HttpRequestMessage createScenario = new(HttpMethod.Post, "api/v1/scenarios")
        {
            Content = JsonContent.Create(EphemeralScenario("admin-delete-scenario"))
        };
        createScenario.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(createScenario)).StatusCode);

        Guid userId;
        using (IServiceScope preScope = _factory.Services.CreateScope())
        {
            FairShareDbContext preDb = preScope.ServiceProvider.GetRequiredService<FairShareDbContext>();
            userId = preDb.Users.Single(u => u.UserName == "admin-deleted-user").Id;
        }

        string adminToken = await LoginAsAdminAsync();
        HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"api/v1/admin/users/{userId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(deleteRequest)).StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        FairShareDbContext db = scope.ServiceProvider.GetRequiredService<FairShareDbContext>();

        Assert.False(db.Users.Any(u => u.Id == userId));
        // Deleted outright - the FK's SetNull must never get the chance to strand
        // ownerless income rows with no retention path.
        Assert.False(db.ParentProfiles.Any(p => p.DisplayName == "Admin Delete Parent"));
        Assert.False(db.Scenarios.Any(s => s.OwnerUserId == userId));
        Assert.False(db.RefreshTokens.Any(t => t.UserId == userId));
    }

    // The CS-42 workbook sample case, as in ScenariosEndpointsTests.
    private static ScenarioSaveRequest EphemeralScenario(string name) => new()
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

    [Fact]
    public async Task DeleteAccount_WithoutTypedConfirmation_Refuses()
    {
        string token = await CreateAndLoginUserAsync("keeping-user");

        HttpRequestMessage request = new(HttpMethod.Delete, "api/v1/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest { Confirm = "yes please" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(request)).StatusCode);

        HttpResponseMessage stillThere = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "keeping-user",
            Password = UserPassword
        });
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_RejectsGuests()
    {
        HttpResponseMessage guestResponse = await _client.PostAsync("api/v1/auth/guest", content: null);
        AuthTokenResponse guest = (await guestResponse.Content.ReadFromJsonAsync<AuthTokenResponse>())!;

        HttpRequestMessage request = new(HttpMethod.Delete, "api/v1/auth/account")
        {
            Content = JsonContent.Create(new DeleteAccountRequest { Confirm = "DELETE" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guest.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request)).StatusCode);
    }

    // ---- Helpers --------------------------------------------------------------------

    private const string UserPassword = "User-Test-12345";

    private static string GetRefreshCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        return Assert.Single(cookies!, c => c.StartsWith("fairshare_refresh=", StringComparison.Ordinal));
    }

    private async Task<string> LoginAsAdminAsync(string? code = null)
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = "admin",
            Password = "Adm!n-Test-12345",
            TwoFactorCode = code
        });

        AuthTokenResponse tokens = (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        return tokens.AccessToken;
    }

    private async Task<string> CreateAndLoginUserAsync(string userName)
    {
        string adminToken = await LoginAsAdminAsync();

        HttpRequestMessage create = new(HttpMethod.Post, "api/v1/admin/users")
        {
            Content = JsonContent.Create(new CreateUserRequest
            {
                UserName = userName,
                Password = UserPassword,
                ConfirmPassword = UserPassword,
                Role = "User"
            })
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        HttpResponseMessage created = await _client.SendAsync(create);
        Assert.True(created.IsSuccessStatusCode);

        HttpResponseMessage login = await _client.PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = UserPassword
        });
        AuthTokenResponse tokens = (await login.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        return tokens.AccessToken;
    }

    private async Task<T?> GetAsAdminAsync<T>(string route, string token)
    {
        HttpRequestMessage request = new(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task<HttpResponseMessage> PostAsAdminAsync<T>(string route, string token, T body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static string ExtractSecret(string authenticatorUri)
    {
        Uri uri = new(authenticatorUri);
        string query = uri.Query.TrimStart('?');

        foreach (string pair in query.Split('&'))
        {
            string[] parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == "secret")
            {
                return parts[1];
            }
        }

        throw new InvalidOperationException("No secret in authenticator URI.");
    }

    // Plain RFC 6238 (30 s step, 6 digits, HMAC-SHA1) against Identity's Base32 key.
    private static string ComputeTotp(string base32Secret)
    {
        byte[] key = Base32Decode(base32Secret);
        long timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

        byte[] message = BitConverter.GetBytes(timestep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(message);
        }

        using HMACSHA1 hmac = new(key);
        byte[] hash = hmac.ComputeHash(message);

        int offset = hash[^1] & 0xf;
        int binary = ((hash[offset] & 0x7f) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.TrimEnd('=').ToUpperInvariant();

        List<byte> output = new(input.Length * 5 / 8);
        int buffer = 0;
        int bits = 0;

        foreach (char c in input)
        {
            int value = alphabet.IndexOf(c);
            if (value < 0)
            {
                continue;
            }

            buffer = (buffer << 5) | value;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xff));
            }
        }

        return [.. output];
    }
}

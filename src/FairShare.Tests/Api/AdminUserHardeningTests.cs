using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using FairShare.Api.Services;
using FairShare.Contracts.Admin;
using FairShare.Contracts.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace FairShare.Tests.Api;

/// <summary>
/// Admin hardening (#148/#149): the seeded admin password is CSPRNG-generated with no
/// predictable shape, and admin user creation returns a DTO rather than the Identity
/// entity (which would serialize PasswordHash, SecurityStamp, and ConcurrencyStamp).
/// </summary>
[Collection("Api")]
public class AdminUserHardeningTests : IClassFixture<FairShareApiFactory>
{
    private readonly HttpClient _client;

    public AdminUserHardeningTests(FairShareApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task CreateUser_ResponseCarriesNoIdentityInternals()
    {
        string adminToken = await LoginAsAdminAsync();

        using HttpRequestMessage request = new(HttpMethod.Post, "api/v1/admin/users")
        {
            Content = JsonContent.Create(new CreateUserRequest
            {
                UserName = "hardening-dto-user",
                Password = "Hardening-12345!",
                ConfirmPassword = "Hardening-12345!",
                Role = "User"
            })
        };
        request.Headers.Add("Authorization", $"Bearer {adminToken}");

        HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync();

        Assert.Contains("hardening-dto-user", json);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concurrencyStamp", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratedPasswords_HaveNoPredictableShape_AndSatisfyPolicy()
    {
        MethodInfo generate = typeof(AdminSeeder)
            .GetMethod("GenerateStrongPassword", BindingFlags.NonPublic | BindingFlags.Static)!;

        HashSet<string> seen = [];

        for (int i = 0; i < 50; i++)
        {
            string pwd = (string)generate.Invoke(null, null)!;

            Assert.Equal(24, pwd.Length);
            Assert.False(pwd.StartsWith("Adm!n"), "the old fixed-prefix shape must be gone");
            Assert.True(pwd.Any(char.IsUpper), "needs an uppercase letter");
            Assert.True(pwd.Any(char.IsLower), "needs a lowercase letter");
            Assert.True(pwd.Any(char.IsDigit), "needs a digit");
            Assert.True(pwd.Any(c => !char.IsLetterOrDigit(c)), "needs a symbol");
            seen.Add(pwd);
        }

        Assert.Equal(50, seen.Count);
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
}

/// <summary>
/// Boots a host that GENERATES the admin password with printing enabled, and asserts the
/// secret reaches stderr but never the logging pipeline - the SQLite sink persists log
/// rows durably and serves them to admins, so the secret must not enter it (#148).
/// </summary>
[Collection("Api")]
public class GeneratedPasswordSeedingTests : IClassFixture<GeneratedPasswordSeedingTests.GeneratedPasswordFactory>
{
    private readonly GeneratedPasswordFactory _factory;

    public GeneratedPasswordSeedingTests(GeneratedPasswordFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void GeneratedPassword_GoesToStderrOnly_NeverTheLoggingPipeline()
    {
        TextWriter originalError = Console.Error;
        using StringWriter stderr = new();
        Console.SetError(stderr);

        try
        {
            // First client access builds and starts the host, which runs the seeder.
            using HttpClient _ = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Contains("generated password:", stderr.ToString());

        Assert.DoesNotContain(_factory.Capture.Messages,
            m => m.Contains("generated password:", StringComparison.OrdinalIgnoreCase));

        // The pipeline still records THAT seeding happened, just without the secret.
        Assert.Contains(_factory.Capture.Messages, m => m.Contains("printed to stderr only"));
    }

    public sealed class GeneratedPasswordFactory : FairShareApiFactory
    {
        public CaptureLoggerProvider Capture { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            SetEnvVar("AdminSeed__Password", "");
            SetEnvVar("AdminSeed__LogGeneratedPassword", "true");
            builder.ConfigureLogging(logging => logging.AddProvider(Capture));
        }
    }

    public sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public ConcurrentBag<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CaptureLogger(ConcurrentBag<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Add(formatter(state, exception));
        }
    }
}

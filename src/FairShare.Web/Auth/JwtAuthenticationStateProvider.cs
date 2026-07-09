using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;

namespace FairShare.Web.Auth;

public class JwtAuthenticationStateProvider(ITokenStore tokenStore) : AuthenticationStateProvider
{
    private readonly ITokenStore _tokenStore = tokenStore;
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string? token = await _tokenStore.GetAccessTokenAsync();

        if (string.IsNullOrWhiteSpace(token))
        {
            return new AuthenticationState(Anonymous);
        }

var claims = JwtParser.ParseClaimsFromJwt(token).ToList();

// Map common JWT claim names to .NET claim types so role/name-based authorization works.
foreach (var c in claims.Where(c => c.Type is "role" or "roles").ToList())
{
    claims.Add(new Claim(ClaimTypes.Role, c.Value));
}

if (claims.All(c => c.Type != ClaimTypes.Name) && claims.FirstOrDefault(c => c.Type == "unique_name") is { } uniqueName)
{
    claims.Add(new Claim(ClaimTypes.Name, uniqueName.Value));
}

if (claims.All(c => c.Type != ClaimTypes.NameIdentifier) && claims.FirstOrDefault(c => c.Type == "sub") is { } sub)
{
    claims.Add(new Claim(ClaimTypes.NameIdentifier, sub.Value));
}
string? exp = claims.FirstOrDefault(c => c.Type == "exp")?.Value;

if (exp is not null && long.TryParse(exp, out long expUnix))
{
    DateTimeOffset expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix);
    if (expiry <= DateTimeOffset.UtcNow)
    {
        return new AuthenticationState(Anonymous);
    }
}

ClaimsIdentity identity = new(claims, authenticationType: "jwt");
return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyAuthenticationChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}

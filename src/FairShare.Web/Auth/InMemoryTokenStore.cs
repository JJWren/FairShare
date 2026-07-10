using System.Threading.Tasks;

namespace FairShare.Web.Auth;

// Kept in memory (not local/sessionStorage) so injected script can't read a persisted
// token; a page reload re-hydrates it from the HttpOnly refresh cookie at startup.
public class InMemoryTokenStore : ITokenStore
{
    private string? _accessToken;

    public Task<string?> GetAccessTokenAsync() => Task.FromResult(_accessToken);

    public Task SetAccessTokenAsync(string accessToken)
    {
        _accessToken = accessToken;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _accessToken = null;
        return Task.CompletedTask;
    }
}

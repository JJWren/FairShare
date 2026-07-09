using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace FairShare.Web.Auth;

// The refresh token is no longer handled here: the API sets it as an HttpOnly
// cookie (scoped to /api/v1/auth) so it's never readable from JS/localStorage.
public class LocalStorageTokenStore(IJSRuntime js) : ITokenStore
{
    private const string AccessTokenKey = "fairshare-access-token";

    private readonly IJSRuntime _js = js;

    public Task<string?> GetAccessTokenAsync() =>
        _js.InvokeAsync<string?>("fairshareStorage.getItem", AccessTokenKey).AsTask();

    public Task SetAccessTokenAsync(string accessToken) =>
        _js.InvokeVoidAsync("fairshareStorage.setItem", AccessTokenKey, accessToken).AsTask();

    public Task ClearAsync() =>
        _js.InvokeVoidAsync("fairshareStorage.removeItem", AccessTokenKey).AsTask();
}

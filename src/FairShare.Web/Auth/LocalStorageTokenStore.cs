using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace FairShare.Web.Auth;

public class LocalStorageTokenStore(IJSRuntime js) : ITokenStore
{
    private const string AccessTokenKey = "fairshare-access-token";
    private const string RefreshTokenKey = "fairshare-refresh-token";

    private readonly IJSRuntime _js = js;

    public Task<string?> GetAccessTokenAsync() =>
        _js.InvokeAsync<string?>("fairshareStorage.getItem", AccessTokenKey).AsTask();

    public Task<string?> GetRefreshTokenAsync() =>
        _js.InvokeAsync<string?>("fairshareStorage.getItem", RefreshTokenKey).AsTask();

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        await _js.InvokeVoidAsync("fairshareStorage.setItem", AccessTokenKey, accessToken);
        await _js.InvokeVoidAsync("fairshareStorage.setItem", RefreshTokenKey, refreshToken);
    }

    public async Task ClearAsync()
    {
        await _js.InvokeVoidAsync("fairshareStorage.removeItem", AccessTokenKey);
        await _js.InvokeVoidAsync("fairshareStorage.removeItem", RefreshTokenKey);
    }
}

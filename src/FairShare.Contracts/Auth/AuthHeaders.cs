namespace FairShare.Contracts.Auth;

/// <summary>
/// Header names shared between the API and its clients, so the two sides can never
/// silently drift apart.
/// </summary>
public static class AuthHeaders
{
    /// <summary>
    /// Required on the anonymous endpoints that set or act on the refresh cookie
    /// (guest/refresh/logout): any custom header forces cross-origin browsers into a
    /// CORS preflight, which the CORS policy refuses - the API's CSRF guard.
    /// </summary>
    public const string CsrfHeaderName = "X-FairShare-Auth";
}

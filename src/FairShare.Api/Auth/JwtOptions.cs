using System;

namespace FairShare.Api.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "FairShare.Api";
    public string Audience { get; set; } = "FairShare.Web";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}

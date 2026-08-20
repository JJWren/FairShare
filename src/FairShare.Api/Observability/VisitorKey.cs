using System;
using System.Security.Cryptography;
using System.Text;

namespace FairShare.Api.Observability;

/// <summary>
/// Computes the anonymous daily visitor key: HMAC-SHA256 over the UTC date, IP, and
/// user-agent. The date inside the payload makes keys unlinkable across days by design
/// (glossary: "Daily visitor"); IP and UA are hash inputs only and are never stored.
/// </summary>
public static class VisitorKey
{
    public static string Compute(byte[] secret, DateOnly utcDay, string ip, string userAgent)
    {
        string payload = $"{utcDay:yyyy-MM-dd}\n{ip}\n{userAgent}";
        byte[] hash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }
}

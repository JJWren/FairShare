using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace FairShare.Web.Auth;

public static class JwtParser
{
    public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        string[] parts = jwt.Split('.');

        if (parts.Length < 2)
        {
            yield break;
        }

        byte[] jsonBytes = ParseBase64WithoutPadding(parts[1]);
        Dictionary<string, object>? payload = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (payload is null)
        {
            yield break;
        }

        foreach (KeyValuePair<string, object> kvp in payload)
        {
            if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    yield return new Claim(kvp.Key, item.ToString());
                }
            }
            else
            {
                yield return new Claim(kvp.Key, kvp.Value?.ToString() ?? string.Empty);
            }
        }
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        string padded = base64.Replace('-', '+').Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }
}

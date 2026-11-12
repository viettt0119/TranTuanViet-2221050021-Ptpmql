using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace DemoMVC.Services;

public static class AuthTokenCodec
{
    public static string Encode(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public static bool TryDecode(string? encodedToken, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(encodedToken))
            return false;

        try
        {
            token = new UTF8Encoding(false, true).GetString(WebEncoders.Base64UrlDecode(encodedToken));
            return !string.IsNullOrEmpty(token);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}

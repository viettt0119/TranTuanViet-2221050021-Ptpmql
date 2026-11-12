using DemoMVC.Services;
using Xunit;

namespace DemoMVC.Tests;

public class AuthTokenCodecTests
{
    [Theory]
    [InlineData("identity-token+/=with symbols")]
    [InlineData("日本語-токен-🔐")]
    public void Encode_and_decode_round_trip(string rawToken)
    {
        var encoded = AuthTokenCodec.Encode(rawToken);

        Assert.DoesNotContain("+", encoded);
        Assert.DoesNotContain("/", encoded);
        Assert.DoesNotContain("=", encoded);
        Assert.True(AuthTokenCodec.TryDecode(encoded, out var decoded));
        Assert.Equal(rawToken, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("***")]
    public void TryDecode_rejects_missing_or_malformed_tokens(string? encodedToken)
    {
        Assert.False(AuthTokenCodec.TryDecode(encodedToken, out var decoded));
        Assert.Empty(decoded);
    }
}

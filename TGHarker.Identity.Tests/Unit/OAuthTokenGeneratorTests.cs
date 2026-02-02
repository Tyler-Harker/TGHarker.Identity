using TGharker.Identity.Web.Services;
using Xunit;

namespace TGHarker.Identity.Tests.Unit;

public class OAuthTokenGeneratorTests
{
    private readonly OAuthTokenGenerator _generator = new();

    [Fact]
    public void GenerateToken_ReturnsBase64UrlEncodedString()
    {
        var token = _generator.GenerateToken();

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        // Base64URL should not contain '+', '/', or '='
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void GenerateToken_ReturnsExpectedLength()
    {
        var token = _generator.GenerateToken();

        // 32 bytes in Base64URL = 43 characters (no padding)
        Assert.Equal(43, token.Length);
    }

    [Fact]
    public void GenerateToken_ReturnsUniqueTokens()
    {
        var tokens = new HashSet<string>();

        for (int i = 0; i < 100; i++)
        {
            var token = _generator.GenerateToken();
            Assert.DoesNotContain(token, tokens);
            tokens.Add(token);
        }
    }

    [Fact]
    public void HashToken_ReturnsBase64UrlEncodedString()
    {
        var token = _generator.GenerateToken();
        var hash = _generator.HashToken(token);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        // Base64URL should not contain '+', '/', or '='
        Assert.DoesNotContain("+", hash);
        Assert.DoesNotContain("/", hash);
        Assert.DoesNotContain("=", hash);
    }

    [Fact]
    public void HashToken_ReturnsDeterministicHash()
    {
        var token = "test-token-value";
        var hash1 = _generator.HashToken(token);
        var hash2 = _generator.HashToken(token);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashToken_ReturnsDifferentHashForDifferentTokens()
    {
        var token1 = "token-1";
        var token2 = "token-2";

        var hash1 = _generator.HashToken(token1);
        var hash2 = _generator.HashToken(token2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashToken_ReturnsExpectedLength()
    {
        var token = _generator.GenerateToken();
        var hash = _generator.HashToken(token);

        // SHA256 = 32 bytes, Base64URL = 43 characters (no padding)
        Assert.Equal(43, hash.Length);
    }

    [Fact]
    public void GeneratedToken_CanBeHashed()
    {
        var token = _generator.GenerateToken();
        var hash = _generator.HashToken(token);

        Assert.NotNull(hash);
        Assert.NotEqual(token, hash);
    }
}

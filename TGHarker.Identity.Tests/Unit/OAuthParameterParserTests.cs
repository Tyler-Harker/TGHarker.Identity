using TGharker.Identity.Web.Services;
using Xunit;

namespace TGHarker.Identity.Tests.Unit;

public class OAuthParameterParserTests
{
    private readonly OAuthParameterParser _parser = new();

    [Fact]
    public void ParseFromQueryString_ParsesBasicParameters()
    {
        var queryString = "client_id=my-client&scope=openid%20profile&redirect_uri=https%3A%2F%2Fexample.com%2Fcallback";

        var result = _parser.ParseFromQueryString(queryString);

        Assert.Equal("my-client", result.ClientId);
        Assert.Equal("openid profile", result.Scope);
        Assert.Equal("https://example.com/callback", result.RedirectUri);
    }

    [Fact]
    public void ParseFromQueryString_HandlesLeadingQuestionMark()
    {
        var queryString = "?client_id=my-client";

        var result = _parser.ParseFromQueryString(queryString);

        Assert.Equal("my-client", result.ClientId);
    }

    [Theory]
    [InlineData("state=abc%2Bdef", "abc+def")]  // %2B decodes to literal +
    [InlineData("state=abc+def", "abc def")]     // literal + decodes to space (per spec)
    [InlineData("state=abc%20def", "abc def")]   // %20 also decodes to space
    public void ParseFromQueryString_HandlesEncodedPlusCorrectly(string query, string expectedState)
    {
        var result = _parser.ParseFromQueryString(query);

        Assert.Equal(expectedState, result.State);
    }

    [Theory]
    [InlineData("code_challenge=abc%2Fdef%3D%3D", "abc/def==")]  // Base64 chars
    [InlineData("code_challenge=abc123", "abc123")]
    public void ParseFromQueryString_HandlesBase64Characters(string query, string expectedCodeChallenge)
    {
        var result = _parser.ParseFromQueryString(query);

        Assert.Equal(expectedCodeChallenge, result.CodeChallenge);
    }

    [Fact]
    public void ParseFromQueryString_ParsesAllOAuthParameters()
    {
        var queryString = "client_id=client&scope=openid&redirect_uri=https://example.com" +
                          "&state=state123&nonce=nonce456&code_challenge=challenge" +
                          "&code_challenge_method=S256&response_mode=form_post&organization_id=org123";

        var result = _parser.ParseFromQueryString(queryString);

        Assert.Equal("client", result.ClientId);
        Assert.Equal("openid", result.Scope);
        Assert.Equal("https://example.com", result.RedirectUri);
        Assert.Equal("state123", result.State);
        Assert.Equal("nonce456", result.Nonce);
        Assert.Equal("challenge", result.CodeChallenge);
        Assert.Equal("S256", result.CodeChallengeMethod);
        Assert.Equal("form_post", result.ResponseMode);
        Assert.Equal("org123", result.OrganizationId);
    }

    [Fact]
    public void ParseFromQueryString_HandlesEmptyParameters()
    {
        var queryString = "client_id=&scope=openid";

        var result = _parser.ParseFromQueryString(queryString);

        Assert.Equal("", result.ClientId);
        Assert.Equal("openid", result.Scope);
    }

    [Fact]
    public void ParseFromQueryString_HandlesMissingParameters()
    {
        var queryString = "client_id=my-client";

        var result = _parser.ParseFromQueryString(queryString);

        Assert.Equal("my-client", result.ClientId);
        Assert.Null(result.Scope);
        Assert.Null(result.State);
    }

    [Fact]
    public void ParseFromReturnUrl_ExtractsQueryString()
    {
        var returnUrl = "/connect/authorize?client_id=my-client&state=test";

        var result = _parser.ParseFromReturnUrl(returnUrl);

        Assert.NotNull(result);
        Assert.Equal("my-client", result.ClientId);
        Assert.Equal("test", result.State);
    }

    [Fact]
    public void ParseFromReturnUrl_ReturnsNullForEmptyUrl()
    {
        var result = _parser.ParseFromReturnUrl(null);
        Assert.Null(result);

        result = _parser.ParseFromReturnUrl("");
        Assert.Null(result);
    }

    [Fact]
    public void ParseFromReturnUrl_ReturnsNullForUrlWithoutQueryString()
    {
        var result = _parser.ParseFromReturnUrl("/connect/authorize");
        Assert.Null(result);
    }

    [Fact]
    public void ParseFromReturnUrl_HandlesEncodedPlusInState()
    {
        // State with Base64 chars that contains a + which should be preserved
        var returnUrl = "/connect/authorize?state=eyJ0%2BeXBlIjoiYXV0aCJ9";

        var result = _parser.ParseFromReturnUrl(returnUrl);

        Assert.NotNull(result);
        Assert.Equal("eyJ0+eXBlIjoiYXV0aCJ9", result.State);
    }

    [Fact]
    public void ParseFromQueryString_HandlesDoubleEncodedValues()
    {
        // If someone double-encodes, we should get the single-decoded value
        // %252B is double-encoded +, which should decode to %2B
        var queryString = "state=%252B";

        var result = _parser.ParseFromQueryString(queryString);

        Assert.Equal("%2B", result.State);
    }
}

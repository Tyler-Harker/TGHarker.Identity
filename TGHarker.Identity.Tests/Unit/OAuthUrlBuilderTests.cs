using TGharker.Identity.Web.Services;
using Xunit;

namespace TGHarker.Identity.Tests.Unit;

public class OAuthUrlBuilderTests
{
    private readonly OAuthUrlBuilder _builder = new();

    [Fact]
    public void BuildUrl_CreatesBasicUrl()
    {
        var parameters = new OAuthParameters
        {
            ClientId = "my-client",
            Scope = "openid"
        };

        var url = _builder.BuildUrl("/tenant/test/login", parameters);

        Assert.StartsWith("/tenant/test/login?", url);
        Assert.Contains("client_id=my-client", url);
        Assert.Contains("scope=openid", url);
    }

    [Theory]
    [InlineData("abc+def", "abc%2Bdef")]      // + must be encoded as %2B
    [InlineData("abc/def=", "abc%2Fdef%3D")]  // Base64 chars must be encoded
    [InlineData("abc def", "abc%20def")]       // space must be encoded as %20
    public void BuildUrl_EncodesStateCorrectly(string state, string expectedEncoded)
    {
        var parameters = new OAuthParameters
        {
            State = state
        };

        var url = _builder.BuildUrl("/test", parameters);

        Assert.Contains($"state={expectedEncoded}", url);
    }

    [Theory]
    [InlineData("abc+def/==", "abc%2Bdef%2F%3D%3D")]  // Common Base64 chars
    public void BuildUrl_EncodesCodeChallengeCorrectly(string challenge, string expectedEncoded)
    {
        var parameters = new OAuthParameters
        {
            CodeChallenge = challenge
        };

        var url = _builder.BuildUrl("/test", parameters);

        Assert.Contains($"code_challenge={expectedEncoded}", url);
    }

    [Fact]
    public void BuildUrl_EncodesRedirectUriCorrectly()
    {
        var parameters = new OAuthParameters
        {
            RedirectUri = "https://example.com/callback?foo=bar"
        };

        var url = _builder.BuildUrl("/test", parameters);

        // The redirect_uri should have its special chars encoded
        Assert.Contains("redirect_uri=https%3A%2F%2Fexample.com%2Fcallback%3Ffoo%3Dbar", url);
    }

    [Fact]
    public void BuildUrl_IncludesAllParameters()
    {
        var parameters = new OAuthParameters
        {
            ClientId = "client",
            Scope = "openid profile",
            RedirectUri = "https://example.com",
            State = "state123",
            Nonce = "nonce456",
            CodeChallenge = "challenge",
            CodeChallengeMethod = "S256",
            ResponseMode = "form_post",
            OrganizationId = "org123"
        };

        var url = _builder.BuildUrl("/test", parameters);

        Assert.Contains("client_id=client", url);
        Assert.Contains("scope=openid%20profile", url);
        Assert.Contains("redirect_uri=https%3A%2F%2Fexample.com", url);
        Assert.Contains("state=state123", url);
        Assert.Contains("nonce=nonce456", url);
        Assert.Contains("code_challenge=challenge", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("response_mode=form_post", url);
        Assert.Contains("organization_id=org123", url);
    }

    [Fact]
    public void BuildUrl_HandlesEmptyParameters()
    {
        var parameters = new OAuthParameters
        {
            ClientId = null,
            Scope = ""
        };

        var url = _builder.BuildUrl("/test", parameters);

        // Empty/null values should still be included but empty
        Assert.Contains("client_id=", url);
        Assert.Contains("scope=", url);
    }

    [Fact]
    public void BuildUrlWithReturnUrl_EncodesReturnUrl()
    {
        var returnUrl = "/connect/authorize?client_id=test&state=abc+123";

        var url = _builder.BuildUrlWithReturnUrl("/tenant/test/login", returnUrl);

        // The whole returnUrl should be encoded
        Assert.StartsWith("/tenant/test/login?returnUrl=", url);
        Assert.Contains("%2Fconnect%2Fauthorize", url);
        // The + in the state should become %2B when the whole URL is encoded
        Assert.Contains("abc%2B123", url);
    }

    [Fact]
    public void BuildUrl_WithReturnUrl_IncludesBoth()
    {
        var parameters = new OAuthParameters
        {
            ClientId = "test-client"
        };

        var url = _builder.BuildUrl("/test", parameters, "/return/path");

        Assert.Contains("client_id=test-client", url);
        Assert.Contains("returnUrl=%2Freturn%2Fpath", url);
    }

    [Fact]
    public void BuildUrl_PreservesBase64PlusCharacter()
    {
        // This is the critical test case for the +/space bug
        // A Base64 state like "eyJ0+eXBlIjoiYXV0aCJ9" should have its + encoded as %2B
        var parameters = new OAuthParameters
        {
            State = "eyJ0+eXBlIjoiYXV0aCJ9"
        };

        var url = _builder.BuildUrl("/test", parameters);

        // The + should be encoded as %2B, NOT left as +
        Assert.Contains("state=eyJ0%2BeXBlIjoiYXV0aCJ9", url);
        // And it should NOT contain an unencoded + (which would decode as space)
        Assert.DoesNotContain("state=eyJ0+", url);
    }

    [Fact]
    public void RoundTrip_PreservesStateWithPlus()
    {
        // Complete round-trip test: build URL, then parse it back
        var originalState = "abc+def/123==";

        var parameters = new OAuthParameters { State = originalState };
        var url = _builder.BuildUrl("/test", parameters);

        // Extract the query string and parse it back
        var queryIndex = url.IndexOf('?');
        var queryString = url[(queryIndex + 1)..];
        var parsed = OAuthParameters.FromQueryString(queryString);

        Assert.Equal(originalState, parsed.State);
    }
}

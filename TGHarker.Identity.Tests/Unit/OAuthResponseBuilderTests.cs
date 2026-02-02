using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TGharker.Identity.Web.Services;
using Xunit;

namespace TGHarker.Identity.Tests.Unit;

public class OAuthResponseBuilderTests
{
    private readonly OAuthResponseBuilder _builder = new();

    [Fact]
    public void CreateSuccessRedirect_QueryMode_AppendsToUri()
    {
        var result = _builder.CreateSuccessRedirect(
            "https://example.com/callback",
            "auth-code-123",
            "state456",
            "query");

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.StartsWith("https://example.com/callback?", redirect.Url);
        Assert.Contains("code=auth-code-123", redirect.Url);
        Assert.Contains("state=state456", redirect.Url);
    }

    [Fact]
    public void CreateSuccessRedirect_QueryMode_AppendsToExistingQueryString()
    {
        var result = _builder.CreateSuccessRedirect(
            "https://example.com/callback?existing=param",
            "auth-code-123",
            "state456",
            "query");

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.Contains("existing=param", redirect.Url);
        Assert.Contains("&code=auth-code-123", redirect.Url);
    }

    [Fact]
    public void CreateSuccessRedirect_FragmentMode_UsesHashSeparator()
    {
        var result = _builder.CreateSuccessRedirect(
            "https://example.com/callback",
            "auth-code-123",
            "state456",
            "fragment");

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.Contains("#code=auth-code-123", redirect.Url);
        Assert.Contains("state=state456", redirect.Url);
    }

    [Fact]
    public void CreateSuccessRedirect_FormPost_ReturnsHtmlContent()
    {
        var result = _builder.CreateSuccessRedirect(
            "https://example.com/callback",
            "auth-code-123",
            "state456",
            "form_post");

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.Equal("text/html", content.ContentType);
        Assert.NotNull(content.ResponseContent);
        Assert.Contains("action=\"https://example.com/callback\"", content.ResponseContent);
        Assert.Contains("name=\"code\" value=\"auth-code-123\"", content.ResponseContent);
        Assert.Contains("name=\"state\" value=\"state456\"", content.ResponseContent);
    }

    [Fact]
    public void CreateSuccessRedirect_NullState_OmitsStateFromResponse()
    {
        var result = _builder.CreateSuccessRedirect(
            "https://example.com/callback",
            "auth-code-123",
            null,
            "query");

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.Contains("code=auth-code-123", redirect.Url);
        Assert.DoesNotContain("state=", redirect.Url);
    }

    [Fact]
    public void CreateErrorRedirect_WithRedirectUri_ReturnsRedirect()
    {
        var result = _builder.CreateErrorRedirect(
            "https://example.com/callback",
            "state123",
            "query",
            "access_denied",
            "User denied the request");

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.Contains("error=access_denied", redirect.Url);
        Assert.Contains("error_description=User+denied+the+request", redirect.Url);
        Assert.Contains("state=state123", redirect.Url);
    }

    [Fact]
    public void CreateErrorRedirect_WithoutRedirectUri_ReturnsBadRequest()
    {
        var result = _builder.CreateErrorRedirect(
            null,
            "state123",
            "query",
            "invalid_client",
            "Unknown client");

        // Result is BadRequest with anonymous type { error, error_description }
        Assert.NotNull(result);
        Assert.Contains("BadRequest", result.GetType().Name);
    }

    [Fact]
    public void CreateErrorRedirect_EmptyRedirectUri_ReturnsBadRequest()
    {
        var result = _builder.CreateErrorRedirect(
            "",
            "state123",
            "query",
            "invalid_client",
            null);

        Assert.NotNull(result);
        Assert.Contains("BadRequest", result.GetType().Name);
    }

    [Fact]
    public void CreateFormPostResponse_HtmlEncodesValues()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["code"] = "<script>alert('xss')</script>",
            ["state"] = "normal-state"
        };

        var result = _builder.CreateFormPostResponse("https://example.com", parameters);

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.NotNull(content.ResponseContent);
        // HTML-encoded script tag
        Assert.Contains("&lt;script&gt;", content.ResponseContent);
        Assert.DoesNotContain("<script>", content.ResponseContent);
    }

    [Fact]
    public void CreateFormPostResponse_HtmlEncodesRedirectUri()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["code"] = "test"
        };

        var result = _builder.CreateFormPostResponse(
            "https://example.com/callback?foo=bar&test=1",
            parameters);

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.NotNull(content.ResponseContent);
        // The & in the action URL should be HTML encoded
        Assert.Contains("action=\"https://example.com/callback?foo=bar&amp;test=1\"", content.ResponseContent);
    }

    [Fact]
    public void CreateFormPostResponse_OmitsNullValues()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["code"] = "auth-code",
            ["state"] = null
        };

        var result = _builder.CreateFormPostResponse("https://example.com", parameters);

        var content = Assert.IsType<ContentHttpResult>(result);
        Assert.NotNull(content.ResponseContent);
        Assert.Contains("name=\"code\"", content.ResponseContent);
        Assert.DoesNotContain("name=\"state\"", content.ResponseContent);
    }

    [Fact]
    public void CreateSuccessRedirect_WithCustomParameters_IncludesAll()
    {
        var parameters = new Dictionary<string, string?>
        {
            ["code"] = "auth-code",
            ["state"] = "state123",
            ["custom_param"] = "custom-value"
        };

        var result = _builder.CreateSuccessRedirect(
            "https://example.com/callback",
            parameters,
            "query");

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.Contains("code=auth-code", redirect.Url);
        Assert.Contains("state=state123", redirect.Url);
        Assert.Contains("custom_param=custom-value", redirect.Url);
    }
}

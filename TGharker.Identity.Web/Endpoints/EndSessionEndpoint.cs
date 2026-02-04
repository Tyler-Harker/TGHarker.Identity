using System.IdentityModel.Tokens.Jwt;
using TGHarker.Identity.Abstractions.Grains;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Endpoints;

public static class EndSessionEndpoint
{
    public static IEndpointRouteBuilder MapEndSessionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/endsession", HandleEndSessionRequest)
            .AllowAnonymous()
            .WithName("EndSession")
            .WithTags("OAuth2");

        // Tenant-prefixed route: /tenant/{tenantId}/connect/endsession
        endpoints.MapGet("/tenant/{tenantId}/connect/endsession", HandleEndSessionRequest)
            .AllowAnonymous()
            .WithName("EndSessionWithTenant")
            .WithTags("OAuth2");

        return endpoints;
    }

    private static async Task<IResult> HandleEndSessionRequest(
        HttpContext context,
        ITenantResolver tenantResolver,
        IClusterClient clusterClient)
    {
        var tenant = await tenantResolver.ResolveAsync(context);
        if (tenant == null)
            return Results.NotFound(new { error = "tenant_not_found" });

        var query = context.Request.Query;
        var idTokenHint = query["id_token_hint"].FirstOrDefault();
        var postLogoutRedirectUri = query["post_logout_redirect_uri"].FirstOrDefault();
        var state = query["state"].FirstOrDefault();

        // If no post_logout_redirect_uri, just show a logged out message
        if (string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            return Results.Content(
                "<html><body><h1>You have been logged out</h1><p>You can close this window.</p></body></html>",
                "text/html");
        }

        // If we have an id_token_hint, validate the post_logout_redirect_uri
        if (!string.IsNullOrEmpty(idTokenHint))
        {
            var clientId = ExtractClientIdFromIdToken(idTokenHint);
            if (!string.IsNullOrEmpty(clientId))
            {
                var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenant.Id}/{clientId}");
                var isValidUri = await clientGrain.ValidatePostLogoutRedirectUriAsync(postLogoutRedirectUri);

                if (!isValidUri)
                {
                    return Results.BadRequest(new
                    {
                        error = "invalid_request",
                        error_description = "The post_logout_redirect_uri is not registered for this client"
                    });
                }
            }
        }

        // Build redirect URL with state if provided
        var redirectUrl = postLogoutRedirectUri;
        if (!string.IsNullOrEmpty(state))
        {
            var separator = redirectUrl.Contains('?') ? "&" : "?";
            redirectUrl = $"{redirectUrl}{separator}state={Uri.EscapeDataString(state)}";
        }

        return Results.Redirect(redirectUrl);
    }

    private static string? ExtractClientIdFromIdToken(string idToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(idToken))
            {
                var token = handler.ReadJwtToken(idToken);
                // The 'aud' (audience) claim contains the client_id
                return token.Audiences.FirstOrDefault();
            }
        }
        catch
        {
            // Invalid token format, ignore
        }

        return null;
    }
}

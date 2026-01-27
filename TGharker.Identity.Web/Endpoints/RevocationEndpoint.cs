using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using TGHarker.Identity.Abstractions.Grains;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Endpoints;

public static class RevocationEndpoint
{
    public static IEndpointRouteBuilder MapRevocationEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/revocation", HandleRevocationRequest)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName("Revocation")
            .WithTags("OAuth2");

        return endpoints;
    }

    private static async Task<IResult> HandleRevocationRequest(
        HttpContext context,
        ITenantResolver tenantResolver,
        IClientAuthenticationService clientAuthService,
        IClusterClient clusterClient)
    {
        var tenant = await tenantResolver.ResolveAsync(context);
        if (tenant == null)
            return Results.NotFound(new { error = "tenant_not_found" });

        // Authenticate client
        var clientResult = await clientAuthService.AuthenticateAsync(context, tenant.Id);
        if (!clientResult.IsSuccess)
        {
            return Results.Json(new { error = "invalid_client" }, statusCode: 401);
        }

        var form = await context.Request.ReadFormAsync();
        var token = form["token"].FirstOrDefault();
        var tokenTypeHint = form["token_type_hint"].FirstOrDefault();

        if (string.IsNullOrEmpty(token))
        {
            // Per RFC 7009, if no token provided, return 200 OK
            return Results.Ok();
        }

        // Try to revoke as refresh token first (or based on hint)
        if (tokenTypeHint == null || tokenTypeHint == "refresh_token")
        {
            var tokenHash = HashToken(token);
            var refreshTokenGrain = clusterClient.GetGrain<IRefreshTokenGrain>($"{tenant.Id}/rt-{tokenHash}");

            var state = await refreshTokenGrain.GetStateAsync();
            if (state != null && state.ClientId == clientResult.Client!.ClientId)
            {
                await refreshTokenGrain.RevokeAsync();
                return Results.Ok();
            }
        }

        // Access tokens are self-contained JWTs and cannot be revoked
        // Could implement a token blacklist if needed

        // Per RFC 7009, always return 200 OK even if token not found
        return Results.Ok();
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Base64UrlEncoder.Encode(hash);
    }
}

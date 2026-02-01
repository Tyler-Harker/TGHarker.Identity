using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;
using TGHarker.Identity.Abstractions.Grains;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Endpoints;

public static class IntrospectionEndpoint
{
    public static IEndpointRouteBuilder MapIntrospectionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/introspect", HandleIntrospectionRequest)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName("Introspection")
            .WithTags("OAuth2");

        // Tenant-prefixed route: /tenant/{tenantId}/connect/introspect
        endpoints.MapPost("/tenant/{tenantId}/connect/introspect", HandleIntrospectionRequest)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName("IntrospectionWithTenant")
            .WithTags("OAuth2");

        return endpoints;
    }

    private static async Task<IResult> HandleIntrospectionRequest(
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
            return Results.Ok(new IntrospectionResponse { Active = false });
        }

        // Try refresh token first if hinted or if it's not a JWT
        if (tokenTypeHint == "refresh_token" || !token.Contains('.'))
        {
            var refreshResult = await IntrospectRefreshTokenAsync(tenant.Id, token, clientResult.Client!.ClientId, clusterClient);
            if (refreshResult != null)
                return Results.Ok(refreshResult);
        }

        // Try access token (JWT)
        if (tokenTypeHint == null || tokenTypeHint == "access_token")
        {
            var accessResult = await IntrospectAccessTokenAsync(tenant.Id, token, clusterClient, context);
            if (accessResult != null)
                return Results.Ok(accessResult);
        }

        return Results.Ok(new IntrospectionResponse { Active = false });
    }

    private static async Task<IntrospectionResponse?> IntrospectRefreshTokenAsync(
        string tenantId,
        string token,
        string clientId,
        IClusterClient clusterClient)
    {
        var tokenHash = HashToken(token);
        var refreshTokenGrain = clusterClient.GetGrain<IRefreshTokenGrain>($"{tenantId}/rt-{tokenHash}");

        var state = await refreshTokenGrain.GetStateAsync();
        if (state == null)
            return null;

        if (state.ClientId != clientId)
            return new IntrospectionResponse { Active = false };

        if (state.IsRevoked || state.ExpiresAt < DateTime.UtcNow)
            return new IntrospectionResponse { Active = false };

        return new IntrospectionResponse
        {
            Active = true,
            TokenType = "refresh_token",
            ClientId = state.ClientId,
            Sub = state.UserId,
            Scope = string.Join(" ", state.Scopes),
            Exp = new DateTimeOffset(state.ExpiresAt).ToUnixTimeSeconds(),
            Iat = new DateTimeOffset(state.CreatedAt).ToUnixTimeSeconds()
        };
    }

    private static async Task<IntrospectionResponse?> IntrospectAccessTokenAsync(
        string tenantId,
        string token,
        IClusterClient clusterClient,
        HttpContext context)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
                return null;

            var jwt = handler.ReadJwtToken(token);

            // Verify token is not expired
            if (jwt.ValidTo < DateTime.UtcNow)
                return new IntrospectionResponse { Active = false };

            // Verify issuer matches tenant
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
            if (jwt.Issuer != baseUrl)
                return new IntrospectionResponse { Active = false };

            // Verify tenant_id claim
            var tokenTenantId = jwt.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
            if (tokenTenantId != tenantId)
                return new IntrospectionResponse { Active = false };

            // Get signing key and verify signature
            var signingKeyGrain = clusterClient.GetGrain<ISigningKeyGrain>($"{tenantId}/signing-keys");
            var publicKeys = await signingKeyGrain.GetPublicKeysAsync();

            var securityKeys = publicKeys.Select(k =>
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(k.PublicKeyPem);
                return new RsaSecurityKey(rsa) { KeyId = k.KeyId };
            }).ToList();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = baseUrl,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKeys = securityKeys,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            handler.ValidateToken(token, validationParameters, out _);

            return new IntrospectionResponse
            {
                Active = true,
                TokenType = "access_token",
                ClientId = jwt.Claims.FirstOrDefault(c => c.Type == "client_id")?.Value,
                Sub = jwt.Subject,
                Scope = jwt.Claims.FirstOrDefault(c => c.Type == "scope")?.Value,
                Exp = new DateTimeOffset(jwt.ValidTo).ToUnixTimeSeconds(),
                Iat = new DateTimeOffset(jwt.IssuedAt).ToUnixTimeSeconds(),
                Iss = jwt.Issuer
            };
        }
        catch
        {
            return new IntrospectionResponse { Active = false };
        }
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Base64UrlEncoder.Encode(hash);
    }
}

public sealed class IntrospectionResponse
{
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("token_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenType { get; set; }

    [JsonPropertyName("client_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientId { get; set; }

    [JsonPropertyName("sub")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sub { get; set; }

    [JsonPropertyName("scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scope { get; set; }

    [JsonPropertyName("exp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Exp { get; set; }

    [JsonPropertyName("iat")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Iat { get; set; }

    [JsonPropertyName("iss")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Iss { get; set; }
}

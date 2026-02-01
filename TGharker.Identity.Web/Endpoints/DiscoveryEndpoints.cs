using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;
using TGHarker.Identity.Abstractions.Grains;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Endpoints;

public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Standard discovery endpoints
        endpoints.MapGet("/.well-known/openid-configuration", HandleDiscoveryRequest)
            .AllowAnonymous()
            .WithName("OpenIdConfiguration")
            .WithTags("Discovery");

        endpoints.MapGet("/.well-known/jwks.json", HandleJwksRequest)
            .AllowAnonymous()
            .WithName("JsonWebKeySet")
            .WithTags("Discovery");

        // Tenant-prefixed discovery endpoints: /{tenantId}/.well-known/...
        endpoints.MapGet("/{tenantId}/.well-known/openid-configuration", HandleDiscoveryRequest)
            .AllowAnonymous()
            .WithName("OpenIdConfigurationWithTenant")
            .WithTags("Discovery");

        endpoints.MapGet("/{tenantId}/.well-known/jwks.json", HandleJwksRequest)
            .AllowAnonymous()
            .WithName("JsonWebKeySetWithTenant")
            .WithTags("Discovery");

        return endpoints;
    }

    private static readonly HashSet<string> KnownRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".well-known", "connect", "account", "admin", "docs", "stats", "tenants", "tenant", "api"
    };

    private static string GetTenantPathPrefix(HttpContext context)
    {
        var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments == null || segments.Length < 2)
            return string.Empty;

        // If first segment is not a known route, it's a tenant prefix
        if (!KnownRoutes.Contains(segments[0]))
        {
            return $"/{segments[0]}";
        }

        return string.Empty;
    }

    private static async Task<IResult> HandleDiscoveryRequest(
        HttpContext context,
        ITenantResolver tenantResolver,
        IClusterClient clusterClient)
    {
        var tenant = await tenantResolver.ResolveAsync(context);
        if (tenant == null)
            return Results.NotFound(new { error = "tenant_not_found" });

        // Check if tenant was resolved via path prefix (e.g., /{tenantId}/.well-known/...)
        var tenantPrefix = GetTenantPathPrefix(context);
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}{tenantPrefix}";

        // Get supported scopes from tenant
        var scopeNames = new List<string> { "openid", "profile", "email", "phone", "offline_access" };

        var document = new DiscoveryDocument
        {
            Issuer = $"{baseUrl}",
            AuthorizationEndpoint = $"{baseUrl}/connect/authorize",
            TokenEndpoint = $"{baseUrl}/connect/token",
            UserInfoEndpoint = $"{baseUrl}/connect/userinfo",
            JwksUri = $"{baseUrl}/.well-known/jwks.json",
            RevocationEndpoint = $"{baseUrl}/connect/revocation",
            IntrospectionEndpoint = $"{baseUrl}/connect/introspect",
            EndSessionEndpoint = $"{baseUrl}/connect/endsession",
            ScopesSupported = scopeNames,
            ClaimsSupported =
            [
                "sub", "name", "given_name", "family_name", "email", "email_verified",
                "phone_number", "phone_number_verified", "picture", "tenant_id"
            ]
        };

        return Results.Ok(document);
    }

    private static async Task<IResult> HandleJwksRequest(
        HttpContext context,
        ITenantResolver tenantResolver,
        IClusterClient clusterClient)
    {
        var tenant = await tenantResolver.ResolveAsync(context);
        if (tenant == null)
            return Results.NotFound(new { error = "tenant_not_found" });

        var signingKeyGrain = clusterClient.GetGrain<ISigningKeyGrain>($"{tenant.Id}/signing-keys");
        var publicKeys = await signingKeyGrain.GetPublicKeysAsync();

        var jwks = new JsonWebKeySetResponse { Keys = [] };

        foreach (var key in publicKeys)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(key.PublicKeyPem);

            var rsaParameters = rsa.ExportParameters(false);
            var jwk = new JsonWebKeyResponse
            {
                Kty = "RSA",
                Use = "sig",
                Kid = key.KeyId,
                Alg = key.Algorithm,
                N = Base64UrlEncoder.Encode(rsaParameters.Modulus),
                E = Base64UrlEncoder.Encode(rsaParameters.Exponent)
            };

            jwks.Keys.Add(jwk);
        }

        return Results.Ok(jwks);
    }
}

public sealed class DiscoveryDocument
{
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("userinfo_endpoint")]
    public string UserInfoEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("jwks_uri")]
    public string JwksUri { get; set; } = string.Empty;

    [JsonPropertyName("revocation_endpoint")]
    public string RevocationEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("introspection_endpoint")]
    public string IntrospectionEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("end_session_endpoint")]
    public string EndSessionEndpoint { get; set; } = string.Empty;

    [JsonPropertyName("scopes_supported")]
    public List<string> ScopesSupported { get; set; } = [];

    [JsonPropertyName("response_types_supported")]
    public List<string> ResponseTypesSupported { get; set; } = ["code"];

    [JsonPropertyName("response_modes_supported")]
    public List<string> ResponseModesSupported { get; set; } = ["query", "fragment", "form_post"];

    [JsonPropertyName("grant_types_supported")]
    public List<string> GrantTypesSupported { get; set; } = ["authorization_code", "client_credentials", "refresh_token"];

    [JsonPropertyName("subject_types_supported")]
    public List<string> SubjectTypesSupported { get; set; } = ["public"];

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public List<string> IdTokenSigningAlgValuesSupported { get; set; } = ["RS256"];

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public List<string> TokenEndpointAuthMethodsSupported { get; set; } = ["client_secret_basic", "client_secret_post"];

    [JsonPropertyName("code_challenge_methods_supported")]
    public List<string> CodeChallengeMethodsSupported { get; set; } = ["S256"];

    [JsonPropertyName("claims_supported")]
    public List<string> ClaimsSupported { get; set; } = [];
}

public sealed class JsonWebKeySetResponse
{
    [JsonPropertyName("keys")]
    public List<JsonWebKeyResponse> Keys { get; set; } = [];
}

public sealed class JsonWebKeyResponse
{
    [JsonPropertyName("kty")]
    public string Kty { get; set; } = string.Empty;

    [JsonPropertyName("use")]
    public string Use { get; set; } = string.Empty;

    [JsonPropertyName("kid")]
    public string Kid { get; set; } = string.Empty;

    [JsonPropertyName("alg")]
    public string Alg { get; set; } = string.Empty;

    [JsonPropertyName("n")]
    public string? N { get; set; }

    [JsonPropertyName("e")]
    public string? E { get; set; }
}

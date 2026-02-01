using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Endpoints;

public static class TokenEndpoint
{
    public static IEndpointRouteBuilder MapTokenEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/token", HandleTokenRequest)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName("Token")
            .WithTags("OAuth2");

        // Tenant-prefixed route: /tenant/{tenantId}/connect/token
        endpoints.MapPost("/tenant/{tenantId}/connect/token", HandleTokenRequest)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName("TokenWithTenant")
            .WithTags("OAuth2");

        return endpoints;
    }

    private static async Task<IResult> HandleTokenRequest(
        HttpContext context,
        ITenantResolver tenantResolver,
        IClientAuthenticationService clientAuthService,
        IJwtTokenGenerator tokenGenerator,
        IClusterClient clusterClient)
    {
        var tenant = await tenantResolver.ResolveAsync(context);
        if (tenant == null)
            return Results.NotFound(new { error = "tenant_not_found" });

        // Authenticate client
        var clientResult = await clientAuthService.AuthenticateAsync(context, tenant.Id);
        if (!clientResult.IsSuccess)
        {
            return Results.Json(new TokenErrorResponse
            {
                Error = clientResult.Error!,
                ErrorDescription = clientResult.ErrorDescription
            }, statusCode: 401);
        }

        var client = clientResult.Client!;
        var form = await context.Request.ReadFormAsync();
        var grantType = form["grant_type"].FirstOrDefault();

        var result = grantType switch
        {
            GrantTypes.AuthorizationCode => await HandleAuthorizationCodeAsync(
                context, tenant, client, form, tokenGenerator, clusterClient),
            GrantTypes.ClientCredentials => await HandleClientCredentialsAsync(
                context, tenant, client, form, tokenGenerator),
            GrantTypes.RefreshToken => await HandleRefreshTokenAsync(
                context, tenant, client, form, tokenGenerator, clusterClient),
            _ => TokenResult.Error("unsupported_grant_type", "The grant type is not supported")
        };

        if (result.IsSuccess)
            return Results.Ok(result.Response);

        return Results.Json(new TokenErrorResponse
        {
            Error = result.ErrorCode!,
            ErrorDescription = result.ErrorDescription
        }, statusCode: 400);
    }

    private static async Task<TokenResult> HandleAuthorizationCodeAsync(
        HttpContext context,
        TenantState tenant,
        ClientState client,
        IFormCollection form,
        IJwtTokenGenerator tokenGenerator,
        IClusterClient clusterClient)
    {
        var code = form["code"].FirstOrDefault();
        var redirectUri = form["redirect_uri"].FirstOrDefault();
        var codeVerifier = form["code_verifier"].FirstOrDefault();

        if (string.IsNullOrEmpty(code))
            return TokenResult.Error("invalid_request", "Code is required");

        if (string.IsNullOrEmpty(redirectUri))
            return TokenResult.Error("invalid_request", "Redirect URI is required");

        // Validate grant type is allowed
        if (!client.AllowedGrantTypes.Contains(GrantTypes.AuthorizationCode))
            return TokenResult.Error("unauthorized_client", "Client is not authorized for this grant type");

        // Hash code to get grain key
        var codeHash = HashCode(code);
        var codeGrain = clusterClient.GetGrain<IAuthorizationCodeGrain>($"{tenant.Id}/code-{codeHash}");

        var authCode = await codeGrain.RedeemAsync(codeVerifier);
        if (authCode == null)
            return TokenResult.Error("invalid_grant", "Invalid authorization code");

        // Validate redirect URI matches
        if (authCode.RedirectUri != redirectUri)
            return TokenResult.Error("invalid_grant", "Redirect URI mismatch");

        // Validate client matches
        if (authCode.ClientId != client.ClientId)
            return TokenResult.Error("invalid_grant", "Client mismatch");

        // Get user
        var userGrain = clusterClient.GetGrain<IUserGrain>($"user-{authCode.UserId}");
        var user = await userGrain.GetStateAsync();
        if (user == null)
            return TokenResult.Error("invalid_grant", "User not found");

        // Get membership for tenant-specific claims
        var membershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenant.Id}/member-{authCode.UserId}");
        var membership = await membershipGrain.GetStateAsync();

        // Issuer must include tenant prefix to match discovery document
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}/tenant/{tenant.Identifier}";

        // Build identity claims based on scopes
        var identityClaims = await BuildIdentityClaimsAsync(user, membership, authCode.Scopes, clusterClient, tenant.Id);
        var additionalClaims = await BuildResourceClaimsAsync(user, membership, authCode.Scopes);

        var tokenContext = new TokenGenerationContext
        {
            TenantId = tenant.Id,
            Issuer = baseUrl,
            Subject = authCode.UserId,
            ClientId = client.ClientId,
            Scopes = authCode.Scopes,
            Nonce = authCode.Nonce,
            AuthTime = authCode.CreatedAt,
            AccessTokenLifetimeMinutes = client.AccessTokenLifetimeMinutes ?? tenant.Configuration.AccessTokenLifetimeMinutes,
            IdTokenLifetimeMinutes = client.IdTokenLifetimeMinutes ?? tenant.Configuration.IdTokenLifetimeMinutes,
            IdentityClaims = identityClaims,
            AdditionalClaims = additionalClaims
        };

        var accessToken = await tokenGenerator.GenerateAccessTokenAsync(tokenContext);
        var idToken = authCode.Scopes.Contains(StandardScopes.OpenId)
            ? await tokenGenerator.GenerateIdTokenAsync(tokenContext)
            : null;

        // Generate refresh token if offline_access scope is present
        string? refreshToken = null;
        if (authCode.Scopes.Contains(StandardScopes.OfflineAccess))
        {
            refreshToken = await CreateRefreshTokenAsync(
                tenant, client, authCode.UserId, authCode.Scopes, context, clusterClient);
        }

        return TokenResult.Success(new TokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = tokenContext.AccessTokenLifetimeMinutes * 60,
            RefreshToken = refreshToken,
            IdToken = idToken,
            Scope = string.Join(" ", authCode.Scopes)
        });
    }

    private static async Task<TokenResult> HandleClientCredentialsAsync(
        HttpContext context,
        TenantState tenant,
        ClientState client,
        IFormCollection form,
        IJwtTokenGenerator tokenGenerator)
    {
        if (!client.IsConfidential)
            return TokenResult.Error("unauthorized_client", "Client credentials grant requires a confidential client");

        if (!client.AllowedGrantTypes.Contains(GrantTypes.ClientCredentials))
            return TokenResult.Error("unauthorized_client", "Client is not authorized for this grant type");

        var requestedScope = form["scope"].FirstOrDefault() ?? string.Empty;
        var scopes = requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Validate scopes
        var validScopes = scopes.Where(s => client.AllowedScopes.Contains(s)).ToList();

        // Issuer must include tenant prefix to match discovery document
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}/tenant/{tenant.Identifier}";

        var tokenContext = new TokenGenerationContext
        {
            TenantId = tenant.Id,
            Issuer = baseUrl,
            Subject = client.ClientId,
            ClientId = client.ClientId,
            Scopes = validScopes,
            AccessTokenLifetimeMinutes = client.AccessTokenLifetimeMinutes ?? tenant.Configuration.AccessTokenLifetimeMinutes
        };

        var accessToken = await tokenGenerator.GenerateAccessTokenAsync(tokenContext);

        return TokenResult.Success(new TokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = tokenContext.AccessTokenLifetimeMinutes * 60,
            Scope = string.Join(" ", validScopes)
        });
    }

    private static async Task<TokenResult> HandleRefreshTokenAsync(
        HttpContext context,
        TenantState tenant,
        ClientState client,
        IFormCollection form,
        IJwtTokenGenerator tokenGenerator,
        IClusterClient clusterClient)
    {
        var refreshTokenValue = form["refresh_token"].FirstOrDefault();
        var requestedScope = form["scope"].FirstOrDefault();

        if (string.IsNullOrEmpty(refreshTokenValue))
            return TokenResult.Error("invalid_request", "Refresh token is required");

        if (!client.AllowedGrantTypes.Contains(GrantTypes.RefreshToken))
            return TokenResult.Error("unauthorized_client", "Client is not authorized for this grant type");

        // Hash token to get grain key
        var tokenHash = HashCode(refreshTokenValue);
        var refreshTokenGrain = clusterClient.GetGrain<IRefreshTokenGrain>($"{tenant.Id}/rt-{tokenHash}");

        var tokenState = await refreshTokenGrain.ValidateAndRevokeAsync();
        if (tokenState == null)
            return TokenResult.Error("invalid_grant", "Invalid refresh token");

        // Validate client matches
        if (tokenState.ClientId != client.ClientId)
            return TokenResult.Error("invalid_grant", "Client mismatch");

        // Determine scopes (requested must be subset of original)
        var scopes = tokenState.Scopes;
        if (!string.IsNullOrEmpty(requestedScope))
        {
            var requestedScopes = requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (requestedScopes.Except(scopes).Any())
                return TokenResult.Error("invalid_scope", "Requested scope exceeds original scope");
            scopes = requestedScopes;
        }

        // Issuer must include tenant prefix to match discovery document
        var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}/tenant/{tenant.Identifier}";
        Dictionary<string, string> identityClaims = [];
        Dictionary<string, string> additionalClaims = [];

        // Get user claims if user-based token
        if (!string.IsNullOrEmpty(tokenState.UserId))
        {
            var userGrain = clusterClient.GetGrain<IUserGrain>($"user-{tokenState.UserId}");
            var user = await userGrain.GetStateAsync();

            var membershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenant.Id}/member-{tokenState.UserId}");
            var membership = await membershipGrain.GetStateAsync();

            if (user != null)
            {
                identityClaims = await BuildIdentityClaimsAsync(user, membership, scopes, clusterClient, tenant.Id);
                additionalClaims = await BuildResourceClaimsAsync(user, membership, scopes);
            }
        }

        var tokenContext = new TokenGenerationContext
        {
            TenantId = tenant.Id,
            Issuer = baseUrl,
            Subject = tokenState.UserId ?? client.ClientId,
            ClientId = client.ClientId,
            Scopes = scopes,
            AccessTokenLifetimeMinutes = client.AccessTokenLifetimeMinutes ?? tenant.Configuration.AccessTokenLifetimeMinutes,
            IdTokenLifetimeMinutes = client.IdTokenLifetimeMinutes ?? tenant.Configuration.IdTokenLifetimeMinutes,
            IdentityClaims = identityClaims,
            AdditionalClaims = additionalClaims
        };

        var accessToken = await tokenGenerator.GenerateAccessTokenAsync(tokenContext);
        var idToken = !string.IsNullOrEmpty(tokenState.UserId) && scopes.Contains(StandardScopes.OpenId)
            ? await tokenGenerator.GenerateIdTokenAsync(tokenContext)
            : null;

        // Create new refresh token (rotation)
        var newRefreshToken = await CreateRefreshTokenAsync(
            tenant, client, tokenState.UserId, scopes, context, clusterClient);

        return TokenResult.Success(new TokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = tokenContext.AccessTokenLifetimeMinutes * 60,
            RefreshToken = newRefreshToken,
            IdToken = idToken,
            Scope = string.Join(" ", scopes)
        });
    }

    private static async Task<string> CreateRefreshTokenAsync(
        TenantState tenant,
        ClientState client,
        string? userId,
        IReadOnlyList<string> scopes,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var refreshTokenValue = GenerateSecureToken();
        var tokenHash = HashCode(refreshTokenValue);

        var refreshTokenGrain = clusterClient.GetGrain<IRefreshTokenGrain>($"{tenant.Id}/rt-{tokenHash}");

        await refreshTokenGrain.CreateAsync(new RefreshTokenState
        {
            TenantId = tenant.Id,
            ClientId = client.ClientId,
            UserId = userId,
            Scopes = scopes.ToList(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(client.RefreshTokenLifetimeDays ?? tenant.Configuration.RefreshTokenLifetimeDays),
            ClientIp = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.FirstOrDefault()
        });

        return refreshTokenValue;
    }

    private static Task<Dictionary<string, string>> BuildIdentityClaimsAsync(
        UserState user,
        TenantMembershipState? membership,
        IReadOnlyList<string> scopes,
        IClusterClient clusterClient,
        string tenantId)
    {
        var claims = new Dictionary<string, string>();

        if (scopes.Contains(StandardScopes.Email))
        {
            claims["email"] = user.Email;
            claims["email_verified"] = user.EmailVerified.ToString().ToLowerInvariant();
        }

        if (scopes.Contains(StandardScopes.Profile))
        {
            if (!string.IsNullOrEmpty(user.GivenName))
                claims["given_name"] = user.GivenName;

            if (!string.IsNullOrEmpty(user.FamilyName))
                claims["family_name"] = user.FamilyName;

            if (!string.IsNullOrEmpty(user.GivenName) || !string.IsNullOrEmpty(user.FamilyName))
                claims["name"] = $"{user.GivenName} {user.FamilyName}".Trim();

            if (!string.IsNullOrEmpty(user.Picture))
                claims["picture"] = user.Picture;

            // Use tenant-specific username if available
            if (!string.IsNullOrEmpty(membership?.Username))
                claims["preferred_username"] = membership.Username;
        }

        if (scopes.Contains(StandardScopes.Phone))
        {
            if (!string.IsNullOrEmpty(user.PhoneNumber))
            {
                claims["phone_number"] = user.PhoneNumber;
                claims["phone_number_verified"] = user.PhoneNumberVerified.ToString().ToLowerInvariant();
            }
        }

        return Task.FromResult(claims);
    }

    private static Task<Dictionary<string, string>> BuildResourceClaimsAsync(
        UserState user,
        TenantMembershipState? membership,
        IReadOnlyList<string> scopes)
    {
        var claims = new Dictionary<string, string>();

        // Add roles from membership
        if (membership != null && membership.Roles.Count > 0)
        {
            claims["roles"] = string.Join(" ", membership.Roles);
        }

        // Add custom claims from membership
        if (membership != null)
        {
            foreach (var claim in membership.Claims)
            {
                claims[claim.Type] = claim.Value;
            }
        }

        return Task.FromResult(claims);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncoder.Encode(bytes);
    }

    private static string HashCode(string code)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code));
        return Base64UrlEncoder.Encode(hash);
    }
}

public sealed class TokenResult
{
    public bool IsSuccess { get; init; }
    public TokenResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDescription { get; init; }

    public static TokenResult Success(TokenResponse response) =>
        new() { IsSuccess = true, Response = response };

    public static TokenResult Error(string error, string? description = null) =>
        new() { IsSuccess = false, ErrorCode = error, ErrorDescription = description };
}

public sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("id_token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdToken { get; set; }

    [JsonPropertyName("scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scope { get; set; }
}

public sealed class TokenErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorDescription { get; set; }
}

using System.Text.Json;
using System.Text.Json.Serialization;
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
        IOAuthTokenGenerator oauthTokenGenerator,
        IClusterClient clusterClient,
        ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation("Token request received. Path: {Path}, Method: {Method}, ContentType: {ContentType}, Host: {Host}, Scheme: {Scheme}",
                context.Request.Path,
                context.Request.Method,
                context.Request.ContentType,
                context.Request.Host,
                context.Request.Scheme);

            var tenant = await tenantResolver.ResolveAsync(context);
            if (tenant == null)
            {
                logger.LogWarning("Tenant not found for token request");
                return Results.NotFound(new { error = "tenant_not_found" });
            }

            logger.LogInformation("Tenant resolved: {TenantId}, Identifier: {TenantIdentifier}", tenant.Id, tenant.Identifier);

            // Authenticate client
            var clientResult = await clientAuthService.AuthenticateAsync(context, tenant.Id);
            if (!clientResult.IsSuccess)
            {
                logger.LogWarning("Client authentication failed: {Error} - {Description}", clientResult.Error, clientResult.ErrorDescription);
                return Results.Json(new TokenErrorResponse
                {
                    Error = clientResult.Error!,
                    ErrorDescription = clientResult.ErrorDescription
                }, statusCode: 401);
            }

            var client = clientResult.Client!;
            logger.LogInformation("Client authenticated: {ClientId}", client.ClientId);

            var form = await context.Request.ReadFormAsync();
            var grantType = form["grant_type"].FirstOrDefault();
            logger.LogInformation("Grant type: {GrantType}", grantType);

            var result = grantType switch
            {
                GrantTypes.AuthorizationCode => await HandleAuthorizationCodeAsync(
                    context, tenant, client, form, tokenGenerator, oauthTokenGenerator, clusterClient, tenantResolver, logger),
                GrantTypes.ClientCredentials => await HandleClientCredentialsAsync(
                    context, tenant, client, form, tokenGenerator, tenantResolver),
                GrantTypes.RefreshToken => await HandleRefreshTokenAsync(
                    context, tenant, client, form, tokenGenerator, oauthTokenGenerator, clusterClient, tenantResolver),
                _ => TokenResult.Error("unsupported_grant_type", "The grant type is not supported")
            };

            if (result.IsSuccess)
            {
                logger.LogInformation("Token request successful for client {ClientId}. Returning JSON response with access_token length: {AccessTokenLength}, id_token present: {IdTokenPresent}",
                    client.ClientId,
                    result.Response?.AccessToken?.Length ?? 0,
                    result.Response?.IdToken != null);
                return Results.Json(result.Response, contentType: "application/json");
            }

            logger.LogWarning("Token request failed: {Error} - {Description}", result.ErrorCode, result.ErrorDescription);
            return Results.Json(new TokenErrorResponse
            {
                Error = result.ErrorCode!,
                ErrorDescription = result.ErrorDescription
            }, statusCode: 400, contentType: "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in token endpoint");
            return Results.Json(new TokenErrorResponse
            {
                Error = "server_error",
                ErrorDescription = ex.Message
            }, statusCode: 500);
        }
    }

    private static async Task<TokenResult> HandleAuthorizationCodeAsync(
        HttpContext context,
        TenantState tenant,
        ClientState client,
        IFormCollection form,
        IJwtTokenGenerator tokenGenerator,
        IOAuthTokenGenerator oauthTokenGenerator,
        IClusterClient clusterClient,
        ITenantResolver tenantResolver,
        ILogger logger)
    {
        var code = form["code"].FirstOrDefault();
        var redirectUri = form["redirect_uri"].FirstOrDefault();
        var codeVerifier = form["code_verifier"].FirstOrDefault();

        logger.LogInformation("Authorization code exchange. Code present: {CodePresent}, RedirectUri: {RedirectUri}, CodeVerifier present: {VerifierPresent}",
            !string.IsNullOrEmpty(code), redirectUri, !string.IsNullOrEmpty(codeVerifier));

        if (string.IsNullOrEmpty(code))
            return TokenResult.Error("invalid_request", "Code is required");

        if (string.IsNullOrEmpty(redirectUri))
            return TokenResult.Error("invalid_request", "Redirect URI is required");

        // Validate grant type is allowed
        if (!client.AllowedGrantTypes.Contains(GrantTypes.AuthorizationCode))
        {
            logger.LogWarning("Client {ClientId} not authorized for authorization_code grant. Allowed: {AllowedGrants}",
                client.ClientId, string.Join(", ", client.AllowedGrantTypes));
            return TokenResult.Error("unauthorized_client", "Client is not authorized for this grant type");
        }

        // Hash code to get grain key
        var codeHash = oauthTokenGenerator.HashToken(code);
        var grainKey = $"{tenant.Id}/code-{codeHash}";
        logger.LogInformation("Looking up authorization code grain: {GrainKey}", grainKey);

        var codeGrain = clusterClient.GetGrain<IAuthorizationCodeGrain>(grainKey);

        var authCode = await codeGrain.RedeemAsync(codeVerifier);
        if (authCode == null)
        {
            logger.LogWarning("Authorization code not found or already redeemed. GrainKey: {GrainKey}", grainKey);
            return TokenResult.Error("invalid_grant", "Invalid authorization code");
        }

        logger.LogInformation("Authorization code redeemed. UserId: {UserId}, Scopes: {Scopes}",
            authCode.UserId, string.Join(" ", authCode.Scopes));

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

        // Use canonical issuer URL for consistency with discovery document
        var baseUrl = tenantResolver.GetIssuerUrl(context, tenant);
        logger.LogInformation("Generating tokens with issuer: {Issuer}", baseUrl);

        // Build identity claims based on scopes
        var identityClaims = await BuildIdentityClaimsAsync(user, membership, authCode.Scopes, clusterClient, tenant.Id, authCode.SelectedOrganizationId);
        var additionalClaims = await BuildResourceClaimsAsync(user, membership, authCode.Scopes, clusterClient, tenant.Id, client.ClientId, authCode.SelectedOrganizationId);

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

        logger.LogInformation("Generating access token for user {UserId}", authCode.UserId);
        var accessToken = await tokenGenerator.GenerateAccessTokenAsync(tokenContext);

        var idToken = authCode.Scopes.Contains(StandardScopes.OpenId)
            ? await tokenGenerator.GenerateIdTokenAsync(tokenContext)
            : null;
        logger.LogInformation("Tokens generated. IdToken present: {IdTokenPresent}", idToken != null);

        // Generate refresh token if offline_access scope is present
        string? refreshToken = null;
        if (authCode.Scopes.Contains(StandardScopes.OfflineAccess))
        {
            // Initial token, not rotation - ignore the hash
            (refreshToken, _) = await CreateRefreshTokenAsync(
                tenant, client, authCode.UserId, authCode.Scopes, context, clusterClient, oauthTokenGenerator);
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
        IJwtTokenGenerator tokenGenerator,
        ITenantResolver tenantResolver)
    {
        if (!client.IsConfidential)
            return TokenResult.Error("unauthorized_client", "Client credentials grant requires a confidential client");

        if (!client.AllowedGrantTypes.Contains(GrantTypes.ClientCredentials))
            return TokenResult.Error("unauthorized_client", "Client is not authorized for this grant type");

        var requestedScope = form["scope"].FirstOrDefault() ?? string.Empty;
        var scopes = requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Validate scopes
        var validScopes = scopes.Where(s => client.AllowedScopes.Contains(s)).ToList();

        // Use canonical issuer URL for consistency with discovery document
        var baseUrl = tenantResolver.GetIssuerUrl(context, tenant);

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
        IOAuthTokenGenerator oauthTokenGenerator,
        IClusterClient clusterClient,
        ITenantResolver tenantResolver)
    {
        var refreshTokenValue = form["refresh_token"].FirstOrDefault();
        var requestedScope = form["scope"].FirstOrDefault();

        if (string.IsNullOrEmpty(refreshTokenValue))
            return TokenResult.Error("invalid_request", "Refresh token is required");

        if (!client.AllowedGrantTypes.Contains(GrantTypes.RefreshToken))
            return TokenResult.Error("unauthorized_client", "Client is not authorized for this grant type");

        // Hash token to get grain key
        var tokenHash = oauthTokenGenerator.HashToken(refreshTokenValue);
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

        // Use canonical issuer URL for consistency with discovery document
        var baseUrl = tenantResolver.GetIssuerUrl(context, tenant);
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
                // For refresh tokens, use the default organization since we don't store selected org in refresh token
                var selectedOrgId = membership?.DefaultOrganizationId;
                identityClaims = await BuildIdentityClaimsAsync(user, membership, scopes, clusterClient, tenant.Id, selectedOrgId);
                additionalClaims = await BuildResourceClaimsAsync(user, membership, scopes, clusterClient, tenant.Id, client.ClientId, selectedOrgId);
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
        var (newRefreshToken, newTokenHash) = await CreateRefreshTokenAsync(
            tenant, client, tokenState.UserId, scopes, context, clusterClient, oauthTokenGenerator);

        // Link old token to new token for token reuse detection
        await refreshTokenGrain.SetReplacementTokenAsync(newTokenHash);

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

    private static async Task<(string TokenValue, string TokenHash)> CreateRefreshTokenAsync(
        TenantState tenant,
        ClientState client,
        string? userId,
        IReadOnlyList<string> scopes,
        HttpContext context,
        IClusterClient clusterClient,
        IOAuthTokenGenerator oauthTokenGenerator)
    {
        var refreshTokenValue = oauthTokenGenerator.GenerateToken();
        var tokenHash = oauthTokenGenerator.HashToken(refreshTokenValue);

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

        return (refreshTokenValue, tokenHash);
    }

    private static async Task<Dictionary<string, string>> BuildIdentityClaimsAsync(
        UserState user,
        TenantMembershipState? membership,
        IReadOnlyList<string> scopes,
        IClusterClient clusterClient,
        string tenantId,
        string? selectedOrganizationId)
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

        // Add organizations claim - user's organization memberships within this tenant
        var tenantOrgMemberships = user.OrganizationMemberships
            .Where(m => m.TenantId == tenantId)
            .ToList();

        if (tenantOrgMemberships.Count > 0)
        {
            var organizations = new List<OrganizationClaimValue>();

            foreach (var orgRef in tenantOrgMemberships)
            {
                var orgGrain = clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{orgRef.OrganizationId}");
                var orgState = await orgGrain.GetStateAsync();

                if (orgState != null && orgState.IsActive)
                {
                    organizations.Add(new OrganizationClaimValue
                    {
                        Id = orgRef.OrganizationId,
                        Name = orgState.Name
                    });
                }
            }

            if (organizations.Count > 0)
            {
                claims["organizations"] = JsonSerializer.Serialize(organizations, OrganizationClaimJsonContext.Default.ListOrganizationClaimValue);
            }
        }

        // Add selected/current organization claim
        if (!string.IsNullOrEmpty(selectedOrganizationId))
        {
            var selectedOrgGrain = clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{selectedOrganizationId}");
            var selectedOrgState = await selectedOrgGrain.GetStateAsync();

            if (selectedOrgState != null && selectedOrgState.IsActive)
            {
                var orgClaim = new OrganizationClaimValue
                {
                    Id = selectedOrganizationId,
                    Name = selectedOrgState.Name
                };
                claims["organization"] = JsonSerializer.Serialize(orgClaim, OrganizationClaimJsonContext.Default.OrganizationClaimValue);
            }
        }

        return claims;
    }

    private static async Task<Dictionary<string, string>> BuildResourceClaimsAsync(
        UserState user,
        TenantMembershipState? membership,
        IReadOnlyList<string> scopes,
        IClusterClient clusterClient,
        string tenantId,
        string clientId,
        string? organizationId)
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

        // Add application-scoped permissions and roles
        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        var clientState = await clientGrain.GetStateAsync();

        if (clientState?.IncludePermissionsInToken == true)
        {
            var userAppRolesGrain = clusterClient.GetGrain<IUserApplicationRolesGrain>(
                $"{tenantId}/client-{clientId}/user-{user.Id}");

            if (await userAppRolesGrain.ExistsAsync())
            {
                // Get effective roles for the current context
                var appRoles = await userAppRolesGrain.GetEffectiveRolesAsync(organizationId);
                if (appRoles.Count > 0)
                {
                    claims["app_roles"] = JsonSerializer.Serialize(appRoles, AppRolesJsonContext.Default.IReadOnlyListString);
                }

                // Get effective permissions for the current context
                var permissions = await userAppRolesGrain.GetEffectivePermissionsAsync(organizationId);
                if (permissions.Count > 0)
                {
                    claims["permissions"] = JsonSerializer.Serialize(permissions, AppRolesJsonContext.Default.IReadOnlySetString);
                }
            }
        }

        return claims;
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

public sealed class OrganizationClaimValue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

[JsonSerializable(typeof(List<OrganizationClaimValue>))]
[JsonSerializable(typeof(OrganizationClaimValue))]
internal partial class OrganizationClaimJsonContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(IReadOnlySet<string>))]
internal partial class AppRolesJsonContext : JsonSerializerContext
{
}

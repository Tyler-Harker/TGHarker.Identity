using System.Web;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Endpoints;

public static class AuthorizeEndpoint
{
    public static IEndpointRouteBuilder MapAuthorizeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/authorize", HandleAuthorizeRequest)
            .AllowAnonymous()
            .WithName("Authorize")
            .WithTags("OAuth2");

        // Tenant-prefixed route: /tenant/{tenantId}/connect/authorize
        endpoints.MapGet("/tenant/{tenantId}/connect/authorize", HandleAuthorizeRequest)
            .AllowAnonymous()
            .WithName("AuthorizeWithTenant")
            .WithTags("OAuth2");

        return endpoints;
    }

    private static async Task<IResult> HandleAuthorizeRequest(
        HttpContext context,
        ITenantResolver tenantResolver,
        IOAuthTokenGenerator oauthTokenGenerator,
        IOAuthUrlBuilder urlBuilder,
        IOAuthResponseBuilder responseBuilder,
        IClusterClient clusterClient)
    {
        var tenant = await tenantResolver.ResolveAsync(context);
        if (tenant == null)
            return Results.NotFound(new { error = "tenant_not_found" });

        var query = context.Request.Query;

        var responseType = query["response_type"].FirstOrDefault();
        var clientId = query["client_id"].FirstOrDefault();
        var redirectUri = query["redirect_uri"].FirstOrDefault();
        var scope = query["scope"].FirstOrDefault();
        var state = query["state"].FirstOrDefault();
        var nonce = query["nonce"].FirstOrDefault();
        var codeChallenge = query["code_challenge"].FirstOrDefault();
        var codeChallengeMethod = query["code_challenge_method"].FirstOrDefault() ?? "S256";
        var responseMode = query["response_mode"].FirstOrDefault() ?? "query";
        var prompt = query["prompt"].FirstOrDefault();
        var loginHint = query["login_hint"].FirstOrDefault();

        // Validate required parameters
        if (string.IsNullOrEmpty(responseType))
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "invalid_request", "response_type is required");

        if (responseType != "code")
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "unsupported_response_type", "Only code response type is supported");

        if (string.IsNullOrEmpty(clientId))
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "invalid_request", "client_id is required");

        if (string.IsNullOrEmpty(redirectUri))
            return responseBuilder.CreateErrorRedirect(null, state, responseMode, "invalid_request", "redirect_uri is required");

        if (string.IsNullOrEmpty(scope))
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "invalid_request", "scope is required");

        // Get client
        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenant.Id}/{clientId}");
        var client = await clientGrain.GetStateAsync();

        if (client == null)
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "invalid_client", "Client not found");

        if (!client.IsActive)
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "invalid_client", "Client is not active");

        // Validate redirect URI format and security
        var uriValidation = RedirectUriValidator.ValidateForAuthorization(redirectUri, client.RedirectUris);
        if (!uriValidation.IsValid)
            return responseBuilder.CreateErrorRedirect(null, state, responseMode, "invalid_request", uriValidation.Error ?? "Invalid redirect_uri");

        // Validate grant type
        if (!await clientGrain.ValidateGrantTypeAsync(GrantTypes.AuthorizationCode))
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "unauthorized_client", "Client not authorized for authorization_code grant");

        // Validate PKCE
        if (client.RequirePkce && string.IsNullOrEmpty(codeChallenge))
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "invalid_request", "PKCE code_challenge is required");

        // Only allow S256 PKCE method - "plain" provides no security benefit
        if (!string.IsNullOrEmpty(codeChallenge) && codeChallengeMethod != "S256")
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "invalid_request", "Only S256 code_challenge_method is supported");

        // Parse and validate scopes
        var requestedScopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var validScopes = new List<string>();

        foreach (var s in requestedScopes)
        {
            if (await clientGrain.ValidateScopeAsync(s))
                validScopes.Add(s);
        }

        if (requestedScopes.Contains(StandardScopes.OpenId) && !validScopes.Contains(StandardScopes.OpenId))
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "invalid_scope", "openid scope is not allowed for this client");

        // Validate nonce for OpenID Connect flows
        // Per OIDC spec, nonce is required for implicit flow (we only support code flow where it's optional but recommended)
        if (requestedScopes.Contains(StandardScopes.OpenId) && string.IsNullOrEmpty(nonce))
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AuthorizeEndpoint");
            logger.LogWarning("OpenID Connect request without nonce for client {ClientId}. Nonce is recommended for replay protection.", clientId);
        }

        // Build OAuth parameters for URL building
        var oauthParams = new OAuthParameters
        {
            ClientId = clientId,
            Scope = scope,
            RedirectUri = redirectUri,
            State = state,
            Nonce = nonce,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            ResponseMode = responseMode
        };

        // Check if user is authenticated
        var userId = context.User.FindFirst("sub")?.Value;
        var userTenantId = context.User.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrEmpty(userId) || userTenantId != tenant.Id)
        {
            // Redirect to tenant-specific login
            var returnUrl = context.Request.Path + context.Request.QueryString;
            var loginUrl = urlBuilder.BuildUrlWithReturnUrl($"/tenant/{tenant.Identifier}/login", returnUrl);
            return Results.Redirect(loginUrl);
        }

        // Check if user has membership in tenant
        var membershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenant.Id}/member-{userId}");
        var membership = await membershipGrain.GetStateAsync();

        if (membership == null || !membership.IsActive)
            return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "access_denied", "User is not a member of this tenant");

        // Get user's organizations in this tenant
        var userGrain = clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();
        var userOrgs = user?.OrganizationMemberships
            .Where(m => m.TenantId == tenant.Id)
            .ToList() ?? [];

        // Determine selected organization
        string? selectedOrgId = context.Request.Query["organization_id"].FirstOrDefault();

        // Check if user needs to set up an organization (has no orgs but client requires one)
        if (userOrgs.Count == 0 && string.IsNullOrEmpty(selectedOrgId) && prompt != "none")
        {
            // Get client's UserFlow settings to see if organizations are required
            var userFlow = await clientGrain.GetUserFlowSettingsAsync();

            // Log for debugging
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AuthorizeEndpoint");
            logger.LogInformation(
                "User {UserId} has no organizations. UserFlow: OrganizationsEnabled={OrganizationsEnabled}, Mode={Mode}",
                userId, userFlow?.OrganizationsEnabled, userFlow?.OrganizationMode);

            // Only redirect to setup for Prompt and AutoCreate modes
            // OptionalPrompt allows users to skip organization creation
            if (userFlow?.OrganizationsEnabled == true &&
                (userFlow.OrganizationMode == OrganizationRegistrationMode.Prompt ||
                 userFlow.OrganizationMode == OrganizationRegistrationMode.AutoCreate))
            {
                // User needs to set up an organization - redirect to setup page
                var setupUrl = urlBuilder.BuildUrl($"/tenant/{tenant.Identifier}/setup-organization", oauthParams);
                return Results.Redirect(setupUrl);
            }
        }

        // If user has organizations but none selected, check if client requires organization selection
        if (userOrgs.Count > 0 && string.IsNullOrEmpty(selectedOrgId))
        {
            // Get client's UserFlow settings to check if organizations are enabled for this client
            var userFlow = await clientGrain.GetUserFlowSettingsAsync();

            // Only redirect to org picker if organizations are enabled for this client
            if (userFlow?.OrganizationsEnabled == true && prompt != "none")
            {
                var orgPickerUrl = urlBuilder.BuildUrl($"/tenant/{tenant.Identifier}/select-organization", oauthParams);
                return Results.Redirect(orgPickerUrl);
            }
            // If organizations are not enabled for this client, proceed without organization selection
            // (selectedOrgId remains null)
        }

        // Validate selected organization if provided
        // The organization_id can be either:
        // - The internal OrganizationId (GUID) from SelectOrganization page
        // - The organization Identifier (slug) from JWT claims
        if (!string.IsNullOrEmpty(selectedOrgId))
        {
            // First, check if it matches any OrganizationId directly (GUID)
            var matchedOrg = userOrgs.FirstOrDefault(o => o.OrganizationId == selectedOrgId);

            // If not found by GUID, try to match by organization Identifier
            if (matchedOrg == null)
            {
                foreach (var orgRef in userOrgs)
                {
                    var orgGrain = clusterClient.GetGrain<IOrganizationGrain>($"{tenant.Id}/org-{orgRef.OrganizationId}");
                    var orgState = await orgGrain.GetStateAsync();
                    if (orgState != null && orgState.IsActive &&
                        string.Equals(orgState.Identifier, selectedOrgId, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedOrg = orgRef;
                        break;
                    }
                }
            }

            if (matchedOrg == null)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AuthorizeEndpoint");
                logger.LogWarning(
                    "Organization validation failed: organization_id={SelectedOrgId} not found in user {UserId} memberships. " +
                    "User has {OrgCount} orgs in tenant {TenantId}: [{OrgIds}]",
                    selectedOrgId,
                    userId,
                    userOrgs.Count,
                    tenant.Id,
                    string.Join(", ", userOrgs.Select(o => o.OrganizationId)));
                return responseBuilder.CreateErrorRedirect(redirectUri, state, responseMode, "access_denied", "User is not a member of the selected organization");
            }

            // Normalize to OrganizationId (GUID) for downstream use
            selectedOrgId = matchedOrg.OrganizationId;
        }

        // Check if consent is required
        if (client.RequireConsent && prompt != "none")
        {
            // Check for existing grant
            // For now, always redirect to consent if required
            var consentParams = oauthParams with { OrganizationId = selectedOrgId };
            var consentUrl = urlBuilder.BuildUrl($"/tenant/{tenant.Identifier}/consent", consentParams);
            return Results.Redirect(consentUrl);
        }

        // Generate authorization code
        var code = oauthTokenGenerator.GenerateToken();
        var codeHash = oauthTokenGenerator.HashToken(code);

        var codeGrain = clusterClient.GetGrain<IAuthorizationCodeGrain>($"{tenant.Id}/code-{codeHash}");

        await codeGrain.CreateAsync(new AuthorizationCodeState
        {
            TenantId = tenant.Id,
            ClientId = clientId,
            UserId = userId,
            RedirectUri = redirectUri,
            Scopes = validScopes,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            Nonce = nonce,
            State = state,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(tenant.Configuration.AuthorizationCodeLifetimeMinutes),
            SelectedOrganizationId = selectedOrgId
        });

        return responseBuilder.CreateSuccessRedirect(redirectUri, code, state, responseMode);
    }
}

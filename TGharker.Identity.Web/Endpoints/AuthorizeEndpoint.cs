using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.IdentityModel.Tokens;
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

        return endpoints;
    }

    private static async Task<IResult> HandleAuthorizeRequest(
        HttpContext context,
        ITenantResolver tenantResolver,
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
            return CreateErrorResponse(redirectUri, state, responseMode, "invalid_request", "response_type is required");

        if (responseType != "code")
            return CreateErrorResponse(redirectUri, state, responseMode, "unsupported_response_type", "Only code response type is supported");

        if (string.IsNullOrEmpty(clientId))
            return CreateErrorResponse(redirectUri, state, responseMode, "invalid_request", "client_id is required");

        if (string.IsNullOrEmpty(redirectUri))
            return CreateErrorResponse(null, state, responseMode, "invalid_request", "redirect_uri is required");

        if (string.IsNullOrEmpty(scope))
            return CreateErrorResponse(redirectUri, state, responseMode, "invalid_request", "scope is required");

        // Get client
        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenant.Id}/{clientId}");
        var client = await clientGrain.GetStateAsync();

        if (client == null)
            return CreateErrorResponse(redirectUri, state, responseMode, "invalid_client", "Client not found");

        if (!client.IsActive)
            return CreateErrorResponse(redirectUri, state, responseMode, "invalid_client", "Client is not active");

        // Validate redirect URI
        if (!await clientGrain.ValidateRedirectUriAsync(redirectUri))
            return CreateErrorResponse(null, state, responseMode, "invalid_request", "Invalid redirect_uri");

        // Validate grant type
        if (!await clientGrain.ValidateGrantTypeAsync(GrantTypes.AuthorizationCode))
            return CreateErrorResponse(redirectUri, state, responseMode, "unauthorized_client", "Client not authorized for authorization_code grant");

        // Validate PKCE
        if (client.RequirePkce && string.IsNullOrEmpty(codeChallenge))
            return CreateErrorResponse(redirectUri, state, responseMode, "invalid_request", "PKCE code_challenge is required");

        if (!string.IsNullOrEmpty(codeChallenge) && codeChallengeMethod != "S256" && codeChallengeMethod != "plain")
            return CreateErrorResponse(redirectUri, state, responseMode, "invalid_request", "Unsupported code_challenge_method");

        // Parse and validate scopes
        var requestedScopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var validScopes = new List<string>();

        foreach (var s in requestedScopes)
        {
            if (await clientGrain.ValidateScopeAsync(s))
                validScopes.Add(s);
        }

        if (requestedScopes.Contains(StandardScopes.OpenId) && !validScopes.Contains(StandardScopes.OpenId))
            return CreateErrorResponse(redirectUri, state, responseMode, "invalid_scope", "openid scope is not allowed for this client");

        // Check if user is authenticated
        var userId = context.User.FindFirst("sub")?.Value;
        var userTenantId = context.User.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrEmpty(userId) || userTenantId != tenant.Id)
        {
            // Redirect to tenant-specific login
            var returnUrl = context.Request.Path + context.Request.QueryString;
            var loginUrl = $"/Tenant/{tenant.Identifier}/Login?returnUrl={HttpUtility.UrlEncode(returnUrl)}";
            return Results.Redirect(loginUrl);
        }

        // Check if user has membership in tenant
        var membershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenant.Id}/member-{userId}");
        var membership = await membershipGrain.GetStateAsync();

        if (membership == null || !membership.IsActive)
            return CreateErrorResponse(redirectUri, state, responseMode, "access_denied", "User is not a member of this tenant");

        // Check if consent is required
        if (client.RequireConsent && prompt != "none")
        {
            // Check for existing grant
            // For now, always redirect to consent if required
            var consentUrl = $"/Consent?client_id={clientId}&scope={HttpUtility.UrlEncode(scope)}&redirect_uri={HttpUtility.UrlEncode(redirectUri)}&state={state}&nonce={nonce}&code_challenge={codeChallenge}&code_challenge_method={codeChallengeMethod}&response_mode={responseMode}&tenant={tenant.Identifier}";
            return Results.Redirect(consentUrl);
        }

        // Generate authorization code
        var code = GenerateSecureCode();
        var codeHash = HashCode(code);

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
            ExpiresAt = DateTime.UtcNow.AddMinutes(tenant.Configuration.AuthorizationCodeLifetimeMinutes)
        });

        // Build redirect URL
        var redirectParams = new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = state
        };

        return CreateSuccessRedirect(redirectUri, redirectParams, responseMode);
    }

    private static IResult CreateErrorResponse(string? redirectUri, string? state, string responseMode, string error, string? description)
    {
        if (string.IsNullOrEmpty(redirectUri))
        {
            return Results.BadRequest(new { error, error_description = description });
        }

        var errorParams = new Dictionary<string, string?>
        {
            ["error"] = error,
            ["error_description"] = description,
            ["state"] = state
        };

        return CreateSuccessRedirect(redirectUri, errorParams, responseMode);
    }

    private static IResult CreateSuccessRedirect(string redirectUri, Dictionary<string, string?> parameters, string responseMode)
    {
        var queryParams = string.Join("&",
            parameters.Where(p => p.Value != null).Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));

        var finalUri = responseMode switch
        {
            "fragment" => $"{redirectUri}#{queryParams}",
            "form_post" => redirectUri, // Would need form_post implementation
            _ => redirectUri.Contains('?') ? $"{redirectUri}&{queryParams}" : $"{redirectUri}?{queryParams}"
        };

        return Results.Redirect(finalUri);
    }

    private static string GenerateSecureCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncoder.Encode(bytes);
    }

    private static string HashCode(string code)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Base64UrlEncoder.Encode(hash);
    }
}

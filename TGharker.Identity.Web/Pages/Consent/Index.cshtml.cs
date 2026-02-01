using System.Security.Cryptography;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Consent;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IClusterClient clusterClient, ILogger<IndexModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true, Name = "client_id")]
    public string? ClientId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Scope { get; set; }

    [BindProperty(SupportsGet = true, Name = "redirect_uri")]
    public string? RedirectUri { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? State { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Nonce { get; set; }

    [BindProperty(SupportsGet = true, Name = "code_challenge")]
    public string? CodeChallenge { get; set; }

    [BindProperty(SupportsGet = true, Name = "code_challenge_method")]
    public string? CodeChallengeMethod { get; set; }

    [BindProperty(SupportsGet = true, Name = "response_mode")]
    public string? ResponseMode { get; set; }

    // Route parameter: /tenant/{tenantId}/consent
    [BindProperty(SupportsGet = true)]
    public string? TenantId { get; set; }

    public string? ClientName { get; set; }
    public string? ClientLogoUri { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ScopeViewModel> RequestedScopes { get; set; } = [];

    public class ScopeViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; }
    }

    // Standard OIDC scopes with their display names and required status
    private static readonly Dictionary<string, (string DisplayName, string? Description, bool IsRequired)> StandardScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openid"] = ("OpenID", "Verify your identity", true),
        ["profile"] = ("Profile", "Access your basic profile information", false),
        ["email"] = ("Email", "Access your email address", false),
        ["address"] = ("Address", "Access your address", false),
        ["phone"] = ("Phone", "Access your phone number", false),
        ["offline_access"] = ("Offline Access", "Stay signed in and refresh tokens", false)
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(ClientId) || string.IsNullOrEmpty(RedirectUri) || string.IsNullOrEmpty(Scope))
        {
            return BadRequest();
        }

        var tenantId = User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Get client
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{ClientId}");
        var client = await clientGrain.GetStateAsync();

        if (client == null)
        {
            ErrorMessage = "Invalid client.";
            return Page();
        }

        ClientName = client.ClientName ?? client.ClientId;

        // Parse and load scopes
        var requestedScopeNames = Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var scopeName in requestedScopeNames)
        {
            var scopeGrain = _clusterClient.GetGrain<IScopeGrain>($"{tenantId}/scope-{scopeName}");
            var scope = await scopeGrain.GetStateAsync();

            if (scope != null)
            {
                RequestedScopes.Add(new ScopeViewModel
                {
                    Name = scope.Name,
                    DisplayName = scope.DisplayName ?? scope.Name,
                    Description = scope.Description,
                    IsRequired = scope.IsRequired
                });
            }
            else if (StandardScopes.TryGetValue(scopeName, out var standardScope))
            {
                // Standard OIDC scope - use predefined values
                RequestedScopes.Add(new ScopeViewModel
                {
                    Name = scopeName,
                    DisplayName = standardScope.DisplayName,
                    Description = standardScope.Description,
                    IsRequired = standardScope.IsRequired
                });
            }
            else
            {
                // Custom scope
                RequestedScopes.Add(new ScopeViewModel
                {
                    Name = scopeName,
                    DisplayName = scopeName,
                    IsRequired = false
                });
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string action, string[] grantedScopes)
    {
        var tenantId = User.FindFirst("tenant_id")?.Value;
        var userId = User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        if (action == "deny")
        {
            // User denied - redirect with error
            return RedirectWithError("access_denied", "The user denied the request.");
        }

        // User granted consent
        var requestedScopeNames = Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

        // Get required scopes that must be included
        var finalScopes = new List<string>();

        foreach (var scopeName in requestedScopeNames)
        {
            // Check if it's a standard required scope (like openid)
            var isStandardRequired = StandardScopes.TryGetValue(scopeName, out var standardScope) && standardScope.IsRequired;

            // Check if scope grain marks it as required
            var scopeGrain = _clusterClient.GetGrain<IScopeGrain>($"{tenantId}/scope-{scopeName}");
            var scope = await scopeGrain.GetStateAsync();
            var isGrainRequired = scope?.IsRequired == true;

            // Include if required (by standard or grain) OR if user granted it
            if (isStandardRequired || isGrainRequired || grantedScopes.Contains(scopeName))
            {
                finalScopes.Add(scopeName);
            }
        }

        _logger.LogInformation("Final scopes for consent: {Scopes}", string.Join(" ", finalScopes));

        // Generate authorization code
        var code = GenerateSecureCode();
        var codeHash = HashCode(code);
        var grainKey = $"{tenantId}/code-{codeHash}";

        _logger.LogInformation("Creating authorization code grain: {GrainKey}", grainKey);

        var codeGrain = _clusterClient.GetGrain<IAuthorizationCodeGrain>(grainKey);

        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var tenant = await tenantGrain.GetStateAsync();

        await codeGrain.CreateAsync(new AuthorizationCodeState
        {
            TenantId = tenantId,
            ClientId = ClientId!,
            UserId = userId,
            RedirectUri = RedirectUri!,
            Scopes = finalScopes,
            CodeChallenge = CodeChallenge,
            CodeChallengeMethod = CodeChallengeMethod,
            Nonce = Nonce,
            State = State,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(tenant?.Configuration.AuthorizationCodeLifetimeMinutes ?? 5)
        });

        _logger.LogInformation("User {UserId} granted consent for client {ClientId} with scopes {Scopes}",
            userId, ClientId, string.Join(" ", finalScopes));

        // Redirect with code
        return RedirectWithCode(code);
    }

    private IActionResult RedirectWithError(string error, string? description)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["error"] = error,
            ["error_description"] = description,
            ["state"] = State
        };

        return BuildRedirect(parameters);
    }

    private IActionResult RedirectWithCode(string code)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["code"] = code,
            ["state"] = State
        };

        return BuildRedirect(parameters);
    }

    private IActionResult BuildRedirect(Dictionary<string, string?> parameters)
    {
        var queryParams = string.Join("&",
            parameters.Where(p => p.Value != null).Select(p => $"{p.Key}={HttpUtility.UrlEncode(p.Value)}"));

        var mode = ResponseMode ?? "query";
        var finalUri = mode switch
        {
            "fragment" => $"{RedirectUri}#{queryParams}",
            _ => RedirectUri!.Contains('?') ? $"{RedirectUri}&{queryParams}" : $"{RedirectUri}?{queryParams}"
        };

        return Redirect(finalUri);
    }

    private static string GenerateSecureCode()
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

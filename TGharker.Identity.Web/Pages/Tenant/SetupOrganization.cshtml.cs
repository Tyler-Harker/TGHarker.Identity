using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Tenant;

[Authorize]
public class SetupOrganizationModel : TenantAuthPageModel
{
    private readonly IUserFlowService _userFlowService;
    private readonly IOrganizationCreationService _organizationCreationService;
    private readonly IOAuthUrlBuilder _urlBuilder;
    private readonly IOAuthParameterParser _parameterParser;

    public SetupOrganizationModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        IUserFlowService userFlowService,
        IOrganizationCreationService organizationCreationService,
        IOAuthUrlBuilder urlBuilder,
        IOAuthParameterParser parameterParser,
        ILogger<SetupOrganizationModel> logger)
        : base(clusterClient, searchService, logger)
    {
        _userFlowService = userFlowService;
        _organizationCreationService = organizationCreationService;
        _urlBuilder = urlBuilder;
        _parameterParser = parameterParser;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public UserFlowSettings? UserFlow { get; set; }

    // OAuth parameters passed through individually to avoid URL encoding issues
    // Note: Use FromQuery with explicit names to match OAuth2 snake_case convention
    [BindProperty(SupportsGet = true)]
    [FromQuery(Name = "client_id")]
    public string? ClientId { get; set; }

    [BindProperty(SupportsGet = true)]
    [FromQuery(Name = "scope")]
    public string? Scope { get; set; }

    [BindProperty(SupportsGet = true)]
    [FromQuery(Name = "redirect_uri")]
    public string? RedirectUri { get; set; }

    [BindProperty(SupportsGet = true)]
    [FromQuery(Name = "state")]
    public string? State { get; set; }

    [BindProperty(SupportsGet = true)]
    [FromQuery(Name = "nonce")]
    public string? Nonce { get; set; }

    [BindProperty(SupportsGet = true)]
    [FromQuery(Name = "code_challenge")]
    public string? CodeChallenge { get; set; }

    [BindProperty(SupportsGet = true)]
    [FromQuery(Name = "code_challenge_method")]
    public string? CodeChallengeMethod { get; set; }

    [BindProperty(SupportsGet = true)]
    [FromQuery(Name = "response_mode")]
    public string? ResponseMode { get; set; }

    public class InputModel
    {
        [Display(Name = "Organization Name")]
        [StringLength(100, ErrorMessage = "Organization name must be less than 100 characters.")]
        public string? OrganizationName { get; set; }

        [Display(Name = "Organization Identifier")]
        [RegularExpression(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$",
            ErrorMessage = "Identifier must be lowercase, contain only letters, numbers, and hyphens")]
        [StringLength(50, ErrorMessage = "Organization identifier must be less than 50 characters.")]
        public string? OrganizationIdentifier { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        var userId = User.FindFirst("sub")?.Value;
        var userTenantId = User.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrEmpty(userId) || userTenantId != Tenant!.Id)
        {
            return RedirectToPage("/Tenant/Login", new { tenantId = TenantId, returnUrl = ReturnUrl });
        }

        // Parse OAuth parameters from ReturnUrl if not already set
        ParseOAuthParametersFromReturnUrl();

        // Check if user already has an organization
        var userGrain = ClusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();

        if (user != null)
        {
            var userOrgs = user.OrganizationMemberships
                .Where(m => m.TenantId == Tenant.Id)
                .ToList();

            if (userOrgs.Count > 0)
            {
                // User already has an organization, redirect to authorize
                return RedirectToAuthorize(userOrgs[0].OrganizationId);
            }
        }

        await LoadUserFlowAsync();

        if (UserFlow == null || !UserFlow.OrganizationsEnabled)
        {
            // No organization required, redirect
            return RedirectToAuthorize(null);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        var userId = User.FindFirst("sub")?.Value;
        var userTenantId = User.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrEmpty(userId) || userTenantId != Tenant!.Id)
        {
            return RedirectToPage("/Tenant/Login", new { tenantId = TenantId, returnUrl = ReturnUrl });
        }

        await LoadUserFlowAsync();

        if (UserFlow == null || !UserFlow.OrganizationsEnabled)
        {
            return RedirectToAuthorize(null);
        }

        // Validate organization name is required for Prompt mode
        if (UserFlow.OrganizationMode == OrganizationRegistrationMode.Prompt &&
            UserFlow.RequireOrganizationName &&
            string.IsNullOrWhiteSpace(Input.OrganizationName))
        {
            ErrorMessage = $"{UserFlow.OrganizationNameLabel ?? "Organization name"} is required.";
            return Page();
        }

        // Get user details for organization creation
        var userGrain = ClusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();

        if (user == null)
        {
            ErrorMessage = "User not found.";
            return Page();
        }

        // Create organization
        var orgResult = await _organizationCreationService.CreateOrganizationForUserAsync(
            Tenant!.Id,
            userId,
            user.Email,
            user.GivenName,
            Input.OrganizationName,
            Input.OrganizationIdentifier,
            UserFlow);

        if (!orgResult.Success)
        {
            ErrorMessage = orgResult.Error ?? "Failed to create organization.";
            return Page();
        }

        Logger.LogInformation(
            "Created organization {OrganizationId} for existing user {UserId}",
            orgResult.OrganizationId, userId);

        return RedirectToAuthorize(orgResult.OrganizationId);
    }

    private void ParseOAuthParametersFromReturnUrl()
    {
        if (string.IsNullOrEmpty(ReturnUrl))
            return;

        // If OAuth parameters are already set (from form post), don't override
        if (!string.IsNullOrEmpty(ClientId))
            return;

        try
        {
            var oauthParams = _parameterParser.ParseFromReturnUrl(ReturnUrl);
            if (oauthParams == null)
                return;

            ClientId = oauthParams.ClientId;
            Scope = oauthParams.Scope;
            RedirectUri = oauthParams.RedirectUri;
            State = oauthParams.State;
            Nonce = oauthParams.Nonce;
            CodeChallenge = oauthParams.CodeChallenge;
            CodeChallengeMethod = oauthParams.CodeChallengeMethod;
            ResponseMode = oauthParams.ResponseMode;

            Logger.LogDebug(
                "Parsed OAuth params from ReturnUrl - ClientId: {ClientId}, State: {State}",
                ClientId, State);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse OAuth parameters from ReturnUrl: {ReturnUrl}", ReturnUrl);
        }
    }

    private IActionResult RedirectToAuthorize(string? organizationId)
    {
        // If we don't have OAuth parameters, fall back to simple redirect
        if (string.IsNullOrEmpty(ClientId))
        {
            Logger.LogWarning("No OAuth parameters available, using fallback redirect");
            return Redirect(GetEffectiveReturnUrl());
        }

        // Build authorize URL with proper encoding
        var oauthParams = new OAuthParameters
        {
            ClientId = ClientId,
            Scope = Scope,
            RedirectUri = RedirectUri,
            State = State,
            Nonce = Nonce,
            CodeChallenge = CodeChallenge,
            CodeChallengeMethod = CodeChallengeMethod,
            ResponseMode = ResponseMode,
            OrganizationId = organizationId
        };

        // Add response_type=code since it's required for authorize endpoint
        var authorizeUrl = $"/tenant/{Tenant!.Identifier}/connect/authorize?response_type=code&" +
            BuildOAuthQueryString(oauthParams);

        Logger.LogInformation("Redirecting to authorize: {AuthorizeUrl}", authorizeUrl);

        return Redirect(authorizeUrl);
    }

    private static string BuildOAuthQueryString(OAuthParameters parameters)
    {
        var parts = new List<string>();

        AppendParam(parts, "client_id", parameters.ClientId);
        AppendParam(parts, "redirect_uri", parameters.RedirectUri);
        AppendParam(parts, "scope", parameters.Scope);
        AppendParam(parts, "state", parameters.State);
        AppendParam(parts, "nonce", parameters.Nonce);
        AppendParam(parts, "code_challenge", parameters.CodeChallenge);
        AppendParam(parts, "code_challenge_method", parameters.CodeChallengeMethod);
        AppendParam(parts, "response_mode", parameters.ResponseMode);
        AppendParam(parts, "organization_id", parameters.OrganizationId);

        return string.Join("&", parts);
    }

    private static void AppendParam(List<string> parts, string name, string? value)
    {
        parts.Add($"{name}={Uri.EscapeDataString(value ?? "")}");
    }

    private async Task LoadUserFlowAsync()
    {
        if (Tenant == null)
            return;

        // Use ClientId to resolve user flow directly if available
        if (!string.IsNullOrEmpty(ClientId))
        {
            UserFlow = await _userFlowService.GetUserFlowForClientAsync(Tenant.Id, ClientId);
        }
        else
        {
            UserFlow = await _userFlowService.ResolveUserFlowFromReturnUrlAsync(Tenant.Id, ReturnUrl);
        }
    }
}

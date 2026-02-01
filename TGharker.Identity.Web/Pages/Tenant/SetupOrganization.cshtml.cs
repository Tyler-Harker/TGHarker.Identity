using System.ComponentModel.DataAnnotations;
using System.Web;
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

    public SetupOrganizationModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        IUserFlowService userFlowService,
        IOrganizationCreationService organizationCreationService,
        ILogger<SetupOrganizationModel> logger)
        : base(clusterClient, searchService, logger)
    {
        _userFlowService = userFlowService;
        _organizationCreationService = organizationCreationService;
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
            // ReturnUrl might be URL-encoded, so decode it first
            var decodedUrl = HttpUtility.UrlDecode(ReturnUrl);

            // Parse query string from the return URL
            var queryIndex = decodedUrl.IndexOf('?');
            if (queryIndex < 0)
                return;

            var queryString = decodedUrl[(queryIndex + 1)..];
            var queryParams = HttpUtility.ParseQueryString(queryString);

            ClientId = queryParams["client_id"];
            Scope = queryParams["scope"];
            RedirectUri = queryParams["redirect_uri"];
            State = queryParams["state"];
            Nonce = queryParams["nonce"];
            CodeChallenge = queryParams["code_challenge"];
            CodeChallengeMethod = queryParams["code_challenge_method"];
            ResponseMode = queryParams["response_mode"];

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

        var authorizeUrl = $"/tenant/{Tenant!.Identifier}/connect/authorize?" +
            $"response_type=code" +
            $"&client_id={HttpUtility.UrlEncode(ClientId)}" +
            $"&redirect_uri={HttpUtility.UrlEncode(RedirectUri)}" +
            $"&scope={HttpUtility.UrlEncode(Scope)}" +
            $"&state={HttpUtility.UrlEncode(State)}" +
            $"&nonce={HttpUtility.UrlEncode(Nonce)}" +
            $"&code_challenge={HttpUtility.UrlEncode(CodeChallenge)}" +
            $"&code_challenge_method={HttpUtility.UrlEncode(CodeChallengeMethod)}" +
            $"&response_mode={HttpUtility.UrlEncode(ResponseMode)}";

        if (!string.IsNullOrEmpty(organizationId))
        {
            authorizeUrl += $"&organization_id={HttpUtility.UrlEncode(organizationId)}";
        }

        Logger.LogInformation("Redirecting to authorize: {AuthorizeUrl}", authorizeUrl);

        return Redirect(authorizeUrl);
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

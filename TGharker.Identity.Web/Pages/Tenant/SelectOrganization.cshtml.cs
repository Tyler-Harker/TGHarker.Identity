using System.Web;
using Microsoft.AspNetCore.Mvc;
using TGHarker.Identity.Abstractions.Grains;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Tenant;

public class SelectOrganizationModel : TenantAuthPageModel
{
    private readonly IOAuthUrlBuilder _urlBuilder;

    public SelectOrganizationModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        IOAuthUrlBuilder urlBuilder,
        ILogger<SelectOrganizationModel> logger)
        : base(clusterClient, searchService, logger)
    {
        _urlBuilder = urlBuilder;
    }

    // OAuth parameters passed through
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

    [BindProperty]
    public string? SelectedOrganizationId { get; set; }

    [BindProperty]
    public bool SetAsDefault { get; set; }

    public List<OrganizationInfo> Organizations { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public class OrganizationInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
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
            // Redirect to login
            var returnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;
            var loginUrl = _urlBuilder.BuildUrlWithReturnUrl($"/tenant/{Tenant!.Identifier}/login", returnUrl);
            return Redirect(loginUrl);
        }

        await LoadUserOrganizationsAsync(userId);

        if (Organizations.Count == 0)
        {
            // No organizations, redirect back to authorize without org
            return RedirectToAuthorize(null);
        }

        if (Organizations.Count == 1)
        {
            // Only one org, auto-select it
            return RedirectToAuthorize(Organizations[0].Id);
        }

        // Pre-select the default organization if set
        var membershipGrain = ClusterClient.GetGrain<ITenantMembershipGrain>($"{Tenant!.Id}/member-{userId}");
        var defaultOrgId = await membershipGrain.GetDefaultOrganizationAsync();
        if (!string.IsNullOrEmpty(defaultOrgId) && Organizations.Any(o => o.Id == defaultOrgId))
        {
            SelectedOrganizationId = defaultOrgId;
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
            return Redirect($"/tenant/{Tenant!.Identifier}/login");
        }

        if (string.IsNullOrEmpty(SelectedOrganizationId))
        {
            await LoadUserOrganizationsAsync(userId);
            ErrorMessage = "Please select an organization.";
            return Page();
        }

        // Validate user is member of selected organization
        var userGrain = ClusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();
        var isMember = user?.OrganizationMemberships
            .Any(m => m.TenantId == Tenant!.Id && m.OrganizationId == SelectedOrganizationId) ?? false;

        if (!isMember)
        {
            await LoadUserOrganizationsAsync(userId);
            ErrorMessage = "You are not a member of the selected organization.";
            return Page();
        }

        // Set as default if requested
        if (SetAsDefault)
        {
            var membershipGrain = ClusterClient.GetGrain<ITenantMembershipGrain>($"{Tenant.Id}/member-{userId}");
            await membershipGrain.SetDefaultOrganizationAsync(SelectedOrganizationId);
        }

        return RedirectToAuthorize(SelectedOrganizationId);
    }

    private async Task LoadUserOrganizationsAsync(string userId)
    {
        var userGrain = ClusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();

        if (user == null) return;

        var orgMemberships = user.OrganizationMemberships
            .Where(m => m.TenantId == Tenant!.Id)
            .ToList();

        foreach (var membership in orgMemberships)
        {
            var orgGrain = ClusterClient.GetGrain<IOrganizationGrain>($"{Tenant!.Id}/org-{membership.OrganizationId}");
            var orgState = await orgGrain.GetStateAsync();

            if (orgState != null && orgState.IsActive)
            {
                Organizations.Add(new OrganizationInfo
                {
                    Id = membership.OrganizationId,
                    Identifier = orgState.Identifier,
                    Name = orgState.Name,
                    DisplayName = orgState.DisplayName,
                    Description = orgState.Description
                });
            }
        }
    }

    private IActionResult RedirectToAuthorize(string? organizationId)
    {
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

        // Build authorize URL with response_type=code prefix
        var authorizeUrl = $"/tenant/{Tenant!.Identifier}/connect/authorize?response_type=code&" +
            BuildOAuthQueryString(oauthParams);

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
}

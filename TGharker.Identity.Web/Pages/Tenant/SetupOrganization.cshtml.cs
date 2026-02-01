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

    public SetupOrganizationModel(
        IClusterClient clusterClient,
        IUserFlowService userFlowService,
        IOrganizationCreationService organizationCreationService,
        ILogger<SetupOrganizationModel> logger)
        : base(clusterClient, logger)
    {
        _userFlowService = userFlowService;
        _organizationCreationService = organizationCreationService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public UserFlowSettings? UserFlow { get; set; }

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
                // User already has an organization, redirect to return URL
                return Redirect(GetEffectiveReturnUrl());
            }
        }

        await LoadUserFlowAsync();

        if (UserFlow == null || !UserFlow.OrganizationsEnabled)
        {
            // No organization required, redirect
            return Redirect(GetEffectiveReturnUrl());
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
            return Redirect(GetEffectiveReturnUrl());
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

        return Redirect(GetEffectiveReturnUrl());
    }

    private async Task LoadUserFlowAsync()
    {
        if (Tenant == null)
            return;

        UserFlow = await _userFlowService.ResolveUserFlowFromReturnUrlAsync(Tenant.Id, ReturnUrl);
    }
}

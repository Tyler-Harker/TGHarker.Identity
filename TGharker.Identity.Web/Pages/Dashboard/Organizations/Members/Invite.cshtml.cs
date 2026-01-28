using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Dashboard.Organizations.Members;

[Authorize]
public class InviteModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly IGrainSearchService _searchService;
    private readonly ILogger<InviteModel> _logger;

    public InviteModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ILogger<InviteModel> logger)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string OrganizationId { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public List<OrganizationRole> AvailableRoles { get; set; } = [];
    public string? InviteUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        public List<string> Roles { get; set; } = [];
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("No user in session");

    public async Task<IActionResult> OnGetAsync(string orgId)
    {
        ViewData["ActivePage"] = "Organizations";
        OrganizationId = orgId;

        var tenantId = GetTenantId();

        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{orgId}");
        var org = await orgGrain.GetStateAsync();

        if (org == null)
        {
            return NotFound();
        }

        OrganizationName = org.DisplayName ?? org.Name;
        AvailableRoles = (await orgGrain.GetRolesAsync()).Where(r => !r.IsSystem || r.Id == WellKnownOrganizationRoles.Member).ToList();

        // Default to member role
        Input.Roles = [WellKnownOrganizationRoles.Member];

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string orgId)
    {
        ViewData["ActivePage"] = "Organizations";
        OrganizationId = orgId;

        var tenantId = GetTenantId();
        var userId = GetUserId();

        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{orgId}");
        var org = await orgGrain.GetStateAsync();

        if (org == null)
        {
            return NotFound();
        }

        OrganizationName = org.DisplayName ?? org.Name;
        AvailableRoles = (await orgGrain.GetRolesAsync()).Where(r => !r.IsSystem || r.Id == WellKnownOrganizationRoles.Member).ToList();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.ToLowerInvariant().Trim();

        // Check if there's already a pending invitation for this email
        var existingInvitation = await _searchService.GetPendingOrganizationInvitationAsync(tenantId, orgId, email);
        if (existingInvitation != null)
        {
            ModelState.AddModelError("Input.Email", "A pending invitation already exists for this email.");
            return Page();
        }

        // Check if user is already a member
        if (org.MemberUserIds.Any(id =>
        {
            var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{id}");
            var user = userGrain.GetStateAsync().Result;
            return user?.Email.Equals(email, StringComparison.OrdinalIgnoreCase) == true;
        }))
        {
            ModelState.AddModelError("Input.Email", "This user is already a member of the organization.");
            return Page();
        }

        // Create invitation
        var invitationId = Guid.CreateVersion7().ToString();
        var invitationGrain = _clusterClient.GetGrain<IOrganizationInvitationGrain>($"{tenantId}/org-{orgId}/invitation-{invitationId}");

        var roles = Input.Roles.Count > 0 ? Input.Roles : [WellKnownOrganizationRoles.Member];

        var result = await invitationGrain.CreateAsync(new CreateOrganizationInvitationRequest
        {
            TenantId = tenantId,
            OrganizationId = orgId,
            Email = email,
            InvitedByUserId = userId,
            Roles = roles
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "Failed to create invitation.";
            return Page();
        }

        _logger.LogInformation("Organization invitation created for {Email} to org {OrgId} in tenant {TenantId}",
            email, orgId, tenantId);

        // Generate invite URL
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        InviteUrl = $"{baseUrl}/Account/AcceptOrgInvite?token={result.Token}";

        TempData["SuccessMessage"] = $"Invitation sent to {email}. Share the link below with them.";

        return Page();
    }
}

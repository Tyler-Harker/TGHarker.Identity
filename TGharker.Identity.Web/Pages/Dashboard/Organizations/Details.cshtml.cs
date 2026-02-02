using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Organizations;

[Authorize(Policy = WellKnownPermissions.OrganizationsView)]
public class DetailsModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public DetailsModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public OrganizationState? Organization { get; set; }
    public List<MemberViewModel> Members { get; set; } = [];
    public List<OrganizationRole> Roles { get; set; } = [];

    public class MemberViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Name { get; set; }
        public List<string> Roles { get; set; } = [];
        public DateTime JoinedAt { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("No user in session");

    public async Task<IActionResult> OnGetAsync(string id)
    {
        ViewData["ActivePage"] = "Organizations";

        var tenantId = GetTenantId();
        var currentUserId = GetUserId();

        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{id}");
        Organization = await orgGrain.GetStateAsync();

        if (Organization == null)
        {
            return NotFound();
        }

        Roles = (await orgGrain.GetRolesAsync()).ToList();

        // Load member details
        foreach (var memberId in Organization.MemberUserIds)
        {
            var membershipGrain = _clusterClient.GetGrain<IOrganizationMembershipGrain>($"{tenantId}/org-{id}/member-{memberId}");
            var membership = await membershipGrain.GetStateAsync();

            var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{memberId}");
            var user = await userGrain.GetStateAsync();

            if (user != null && membership != null)
            {
                Members.Add(new MemberViewModel
                {
                    UserId = memberId,
                    Email = user.Email,
                    Name = string.IsNullOrEmpty(user.GivenName) ? null : $"{user.GivenName} {user.FamilyName}".Trim(),
                    Roles = membership.Roles,
                    JoinedAt = membership.JoinedAt,
                    IsCurrentUser = memberId == currentUserId
                });
            }
        }

        Members = Members
            .OrderByDescending(m => m.Roles.Contains(WellKnownOrganizationRoles.Owner))
            .ThenByDescending(m => m.Roles.Contains(WellKnownOrganizationRoles.Admin))
            .ThenBy(m => m.Email)
            .ToList();

        return Page();
    }
}

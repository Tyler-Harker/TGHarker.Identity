using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Roles;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(IClusterClient clusterClient, ILogger<DetailsModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public TenantRole Role { get; set; } = default!;
    public List<MemberInfo> Members { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public class MemberInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Name { get; set; }
    }

    public IReadOnlyList<(string Permission, string Category, string Description)> AvailablePermissions
        => WellKnownPermissions.All;

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task<IActionResult> OnGetAsync(string id)
    {
        ViewData["ActivePage"] = "Roles";

        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("./Index");
        }

        var tenantId = GetTenantId();
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);

        var role = await tenantGrain.GetRoleAsync(id);
        if (role == null)
        {
            return RedirectToPage("./Index");
        }

        Role = role;

        // Load members with this role
        var memberIds = await tenantGrain.GetMemberUserIdsAsync();
        foreach (var userId in memberIds)
        {
            var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
            var membership = await membershipGrain.GetStateAsync();

            if (membership != null && membership.IsActive && membership.Roles.Contains(id))
            {
                var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
                var user = await userGrain.GetStateAsync();

                if (user != null)
                {
                    Members.Add(new MemberInfo
                    {
                        UserId = userId,
                        Email = user.Email,
                        Name = string.IsNullOrEmpty(user.GivenName) ? null : $"{user.GivenName} {user.FamilyName}".Trim()
                    });
                }
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        ViewData["ActivePage"] = "Roles";

        var tenantId = GetTenantId();
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);

        var role = await tenantGrain.GetRoleAsync(id);
        if (role == null)
        {
            TempData["ErrorMessage"] = "Role not found.";
            return RedirectToPage("./Index");
        }

        if (role.IsSystem)
        {
            TempData["ErrorMessage"] = "System roles cannot be deleted.";
            return RedirectToPage("./Details", new { id });
        }

        // Check if any users have this role
        var memberIds = await tenantGrain.GetMemberUserIdsAsync();
        foreach (var userId in memberIds)
        {
            var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
            var membership = await membershipGrain.GetStateAsync();
            if (membership != null && membership.Roles.Contains(id))
            {
                TempData["ErrorMessage"] = "Cannot delete role while users are assigned to it.";
                return RedirectToPage("./Details", new { id });
            }
        }

        var deleted = await tenantGrain.DeleteRoleAsync(id);
        if (!deleted)
        {
            TempData["ErrorMessage"] = "Failed to delete role.";
            return RedirectToPage("./Details", new { id });
        }

        _logger.LogInformation("Role {RoleId} deleted from tenant {TenantId}", id, tenantId);
        TempData["SuccessMessage"] = $"Role '{role.Name}' deleted successfully.";
        return RedirectToPage("./Index");
    }
}

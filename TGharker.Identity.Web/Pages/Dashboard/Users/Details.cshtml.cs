using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Users;

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

    public UserViewModel UserInfo { get; set; } = default!;
    public List<RoleInfo> UserRoles { get; set; } = [];
    public bool IsCurrentUser { get; set; }

    public class UserViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
        public string? DisplayName => string.IsNullOrEmpty(GivenName) ? null : $"{GivenName} {FamilyName}".Trim();
        public DateTime JoinedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class RoleInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystem { get; set; }
        public List<string> Permissions { get; set; } = [];
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("No user in session");

    public async Task<IActionResult> OnGetAsync(string id)
    {
        ViewData["ActivePage"] = "Users";

        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("./Index");
        }

        var tenantId = GetTenantId();
        var currentUserId = GetUserId();

        // Load user
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{id}");
        var user = await userGrain.GetStateAsync();

        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToPage("./Index");
        }

        // Load membership
        var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{id}");
        var membership = await membershipGrain.GetStateAsync();

        if (membership == null || !membership.IsActive)
        {
            TempData["ErrorMessage"] = "User is not a member of this tenant.";
            return RedirectToPage("./Index");
        }

        // Load tenant roles
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var allRoles = await tenantGrain.GetRolesAsync();

        UserInfo = new UserViewModel
        {
            UserId = id,
            Email = user.Email,
            GivenName = user.GivenName,
            FamilyName = user.FamilyName,
            JoinedAt = membership.JoinedAt,
            IsActive = membership.IsActive
        };

        UserRoles = membership.Roles
            .Select(roleId => allRoles.FirstOrDefault(r => r.Id == roleId))
            .Where(r => r != null)
            .Select(r => new RoleInfo
            {
                Id = r!.Id,
                Name = r.Name,
                Description = r.Description,
                IsSystem = r.IsSystem,
                Permissions = r.Permissions
            })
            .ToList();

        IsCurrentUser = id == currentUserId;

        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync(string id)
    {
        ViewData["ActivePage"] = "Users";

        var tenantId = GetTenantId();
        var currentUserId = GetUserId();

        if (id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot remove yourself from the tenant.";
            return RedirectToPage("./Details", new { id });
        }

        // Get user email for message
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{id}");
        var user = await userGrain.GetStateAsync();
        var userEmail = user?.Email ?? id;

        // Deactivate membership
        var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{id}");
        await membershipGrain.DeactivateAsync();

        // Remove from tenant's member list
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        await tenantGrain.RemoveMemberAsync(id);

        // Remove tenant from user's memberships
        await userGrain.RemoveTenantMembershipAsync(tenantId);

        _logger.LogInformation("User {UserId} removed from tenant {TenantId}", id, tenantId);

        TempData["SuccessMessage"] = $"User {userEmail} has been removed from the tenant.";
        return RedirectToPage("./Index");
    }
}

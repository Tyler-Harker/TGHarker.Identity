using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Users;

[Authorize]
public class EditModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<EditModel> _logger;

    public EditModel(IClusterClient clusterClient, ILogger<EditModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public List<TenantRole> AvailableRoles { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        public List<string> SelectedRoles { get; set; } = [];
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

        var currentUserId = GetUserId();
        if (id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot edit your own roles.";
            return RedirectToPage("./Details", new { id });
        }

        var tenantId = GetTenantId();

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
        var roles = await tenantGrain.GetRolesAsync();

        UserId = id;
        UserEmail = user.Email;
        UserName = string.IsNullOrEmpty(user.GivenName) ? null : $"{user.GivenName} {user.FamilyName}".Trim();
        AvailableRoles = roles.ToList();
        Input.SelectedRoles = membership.Roles.ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        ViewData["ActivePage"] = "Users";

        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("./Index");
        }

        var currentUserId = GetUserId();
        if (id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot edit your own roles.";
            return RedirectToPage("./Details", new { id });
        }

        var tenantId = GetTenantId();

        // Load user for display
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{id}");
        var user = await userGrain.GetStateAsync();
        UserId = id;
        UserEmail = user?.Email ?? id;
        UserName = user != null && !string.IsNullOrEmpty(user.GivenName)
            ? $"{user.GivenName} {user.FamilyName}".Trim()
            : null;

        // Load available roles
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var roles = await tenantGrain.GetRolesAsync();
        AvailableRoles = roles.ToList();

        // Validate at least one role selected
        if (Input.SelectedRoles == null || Input.SelectedRoles.Count == 0)
        {
            ErrorMessage = "User must have at least one role.";
            return Page();
        }

        // Load membership
        var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{id}");
        var membership = await membershipGrain.GetStateAsync();

        if (membership == null || !membership.IsActive)
        {
            TempData["ErrorMessage"] = "User is not a member of this tenant.";
            return RedirectToPage("./Index");
        }

        // Update roles
        var currentRoles = membership.Roles.ToList();
        var selectedRoles = Input.SelectedRoles;

        // Remove roles that are no longer selected
        foreach (var roleId in currentRoles)
        {
            if (!selectedRoles.Contains(roleId))
            {
                await membershipGrain.RemoveRoleAsync(roleId);
            }
        }

        // Add newly selected roles
        foreach (var roleId in selectedRoles)
        {
            if (!currentRoles.Contains(roleId))
            {
                await membershipGrain.AddRoleAsync(roleId);
            }
        }

        // If Owner is selected, also ensure Admin is selected
        if (selectedRoles.Contains(WellKnownRoles.TenantOwner) && !selectedRoles.Contains(WellKnownRoles.TenantAdmin))
        {
            await membershipGrain.AddRoleAsync(WellKnownRoles.TenantAdmin);
        }

        _logger.LogInformation("User {UserId} roles updated in tenant {TenantId}: {Roles}",
            id, tenantId, string.Join(", ", selectedRoles));

        TempData["SuccessMessage"] = $"Roles updated for {UserEmail}.";
        return RedirectToPage("./Details", new { id });
    }
}

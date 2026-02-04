using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients.UserRoles;

[Authorize(Policy = WellKnownPermissions.ClientsEdit)]
public class AssignModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public AssignModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    public List<UserViewModel> AvailableUsers { get; set; } = [];
    public UserViewModel? SelectedUser { get; set; }
    public List<ApplicationRoleAssignment> CurrentAssignments { get; set; } = [];
    public List<ApplicationRole> AvailableRoles { get; set; } = [];
    public Dictionary<string, string> RoleNames { get; set; } = [];
    public Dictionary<string, string> OrganizationNames { get; set; } = [];
    public Dictionary<string, string> UserOrganizations { get; set; } = [];

    public class UserViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task<IActionResult> OnGetAsync(string clientId, string? userId)
    {
        ViewData["ActivePage"] = "Clients";

        if (string.IsNullOrEmpty(clientId))
            return RedirectToPage("../Index");

        var tenantId = GetTenantId();
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        var client = await clientGrain.GetStateAsync();

        if (client == null || !client.IsActive)
            return RedirectToPage("../Index");

        ClientId = clientId;
        ClientName = client.ClientName ?? clientId;

        // Load roles
        var roles = await clientGrain.GetApplicationRolesAsync();
        AvailableRoles = roles.OrderBy(r => r.Name).ToList();
        RoleNames = roles.ToDictionary(r => r.Id, r => r.DisplayName ?? r.Name);

        if (!string.IsNullOrEmpty(userId))
        {
            await LoadSelectedUserAsync(tenantId, clientId, userId);
        }
        else if (!string.IsNullOrEmpty(SearchQuery))
        {
            await SearchUsersAsync(tenantId);
        }

        return Page();
    }

    private async Task LoadSelectedUserAsync(string tenantId, string clientId, string userId)
    {
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();

        if (user == null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return;
        }

        SelectedUser = new UserViewModel
        {
            UserId = userId,
            UserName = $"{user.GivenName} {user.FamilyName}".Trim(),
            UserEmail = user.Email
        };

        if (string.IsNullOrEmpty(SelectedUser.UserName))
            SelectedUser.UserName = user.Email;

        // Load current assignments
        var userAppRolesGrain = _clusterClient.GetGrain<IUserApplicationRolesGrain>(
            $"{tenantId}/client-{clientId}/user-{userId}");
        CurrentAssignments = (await userAppRolesGrain.GetRoleAssignmentsAsync()).ToList();

        // Load organization names for display
        var orgIds = CurrentAssignments
            .Where(a => a.Scope == ApplicationRoleScope.Organization && !string.IsNullOrEmpty(a.OrganizationId))
            .Select(a => a.OrganizationId!)
            .Distinct();

        foreach (var orgId in orgIds)
        {
            var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{orgId}");
            var org = await orgGrain.GetStateAsync();
            if (org != null)
            {
                OrganizationNames[orgId] = org.Name;
            }
        }

        // Load user's organization memberships for the assignment dropdown
        var userOrgMemberships = user.OrganizationMemberships
            .Where(m => m.TenantId == tenantId)
            .ToList();

        foreach (var membership in userOrgMemberships)
        {
            var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{membership.OrganizationId}");
            var org = await orgGrain.GetStateAsync();
            if (org != null && org.IsActive)
            {
                UserOrganizations[membership.OrganizationId] = org.Name;
            }
        }
    }

    private async Task SearchUsersAsync(string tenantId)
    {
        var membershipGrains = await _clusterClient.Search<ITenantMembershipGrain>()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .ToListAsync();

        foreach (var membershipGrain in membershipGrains.Take(20))
        {
            var membership = await membershipGrain.GetStateAsync();
            if (membership == null) continue;

            var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{membership.UserId}");
            var user = await userGrain.GetStateAsync();
            if (user == null) continue;

            var userName = $"{user.GivenName} {user.FamilyName}".Trim();
            if (string.IsNullOrEmpty(userName))
                userName = user.Email;

            // Filter by search query
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                var searchLower = SearchQuery.ToLowerInvariant();
                if (!userName.ToLowerInvariant().Contains(searchLower) &&
                    !user.Email.ToLowerInvariant().Contains(searchLower))
                {
                    continue;
                }
            }

            AvailableUsers.Add(new UserViewModel
            {
                UserId = membership.UserId,
                UserName = userName,
                UserEmail = user.Email
            });
        }

        AvailableUsers = AvailableUsers.OrderBy(u => u.UserName).Take(10).ToList();
    }

    public async Task<IActionResult> OnPostAssignAsync(string clientId, string userId, string roleId, int scope, string? organizationId)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleId))
        {
            TempData["ErrorMessage"] = "Invalid request.";
            return RedirectToPage(new { clientId, userId });
        }

        var roleScope = (ApplicationRoleScope)scope;
        if (roleScope == ApplicationRoleScope.Organization && string.IsNullOrEmpty(organizationId))
        {
            TempData["ErrorMessage"] = "Organization is required for organization-scoped roles.";
            return RedirectToPage(new { clientId, userId });
        }

        var tenantId = GetTenantId();
        var userAppRolesGrain = _clusterClient.GetGrain<IUserApplicationRolesGrain>(
            $"{tenantId}/client-{clientId}/user-{userId}");

        await userAppRolesGrain.AssignRoleAsync(roleId, roleScope, organizationId);

        TempData["SuccessMessage"] = "Role has been assigned.";
        return RedirectToPage(new { clientId, userId });
    }

    public async Task<IActionResult> OnPostRemoveAsync(string clientId, string userId, string roleId, string? organizationId)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleId))
        {
            TempData["ErrorMessage"] = "Invalid request.";
            return RedirectToPage(new { clientId, userId });
        }

        var tenantId = GetTenantId();
        var userAppRolesGrain = _clusterClient.GetGrain<IUserApplicationRolesGrain>(
            $"{tenantId}/client-{clientId}/user-{userId}");

        await userAppRolesGrain.RemoveRoleAsync(roleId, organizationId);

        TempData["SuccessMessage"] = "Role assignment has been removed.";
        return RedirectToPage(new { clientId, userId });
    }
}

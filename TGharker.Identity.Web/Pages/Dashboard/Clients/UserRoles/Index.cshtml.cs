using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients.UserRoles;

[Authorize(Policy = WellKnownPermissions.ClientsView)]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public IndexModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public List<UserAssignmentViewModel> UserAssignments { get; set; } = [];
    public Dictionary<string, string> RoleNames { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public class UserAssignmentViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public List<ApplicationRoleAssignment> Assignments { get; set; } = [];
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task<IActionResult> OnGetAsync(string clientId)
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

        // Build role name lookup
        var roles = await clientGrain.GetApplicationRolesAsync();
        RoleNames = roles.ToDictionary(r => r.Id, r => r.DisplayName ?? r.Name);

        // Get all tenant members and check for application role assignments
        var membershipGrains = await _clusterClient.Search<ITenantMembershipGrain>()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .ToListAsync();

        var usersWithAssignments = new List<UserAssignmentViewModel>();

        foreach (var membershipGrain in membershipGrains)
        {
            var membership = await membershipGrain.GetStateAsync();
            if (membership == null) continue;

            var userAppRolesGrain = _clusterClient.GetGrain<IUserApplicationRolesGrain>(
                $"{tenantId}/client-{clientId}/user-{membership.UserId}");

            if (!await userAppRolesGrain.ExistsAsync())
                continue;

            var assignments = await userAppRolesGrain.GetRoleAssignmentsAsync();
            if (assignments.Count == 0)
                continue;

            // Get user details
            var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{membership.UserId}");
            var user = await userGrain.GetStateAsync();

            usersWithAssignments.Add(new UserAssignmentViewModel
            {
                UserId = membership.UserId,
                UserName = user != null ? $"{user.GivenName} {user.FamilyName}".Trim() : membership.Username ?? membership.UserId,
                UserEmail = user?.Email ?? string.Empty,
                Assignments = assignments.ToList()
            });
        }

        TotalCount = usersWithAssignments.Count;

        UserAssignments = usersWithAssignments
            .OrderBy(u => u.UserName)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        return Page();
    }
}

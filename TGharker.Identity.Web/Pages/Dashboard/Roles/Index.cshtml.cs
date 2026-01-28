using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Roles;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public IndexModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public List<RoleViewModel> Roles { get; set; } = [];

    public class RoleViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystem { get; set; }
        public List<string> Permissions { get; set; } = [];
        public int MemberCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task OnGetAsync()
    {
        ViewData["ActivePage"] = "Roles";
        await LoadRolesAsync();
    }

    private async Task LoadRolesAsync()
    {
        var tenantId = GetTenantId();
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);

        var roles = await tenantGrain.GetRolesAsync();
        var memberIds = await tenantGrain.GetMemberUserIdsAsync();

        // Count members per role
        var roleMemberCounts = new Dictionary<string, int>();
        foreach (var userId in memberIds)
        {
            var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
            var membership = await membershipGrain.GetStateAsync();
            if (membership != null && membership.IsActive)
            {
                foreach (var roleId in membership.Roles)
                {
                    roleMemberCounts.TryAdd(roleId, 0);
                    roleMemberCounts[roleId]++;
                }
            }
        }

        Roles = roles.Select(r => new RoleViewModel
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            IsSystem = r.IsSystem,
            Permissions = r.Permissions,
            MemberCount = roleMemberCounts.GetValueOrDefault(r.Id, 0),
            CreatedAt = r.CreatedAt
        })
        .OrderByDescending(r => r.IsSystem)
        .ThenBy(r => r.Name)
        .ToList();
    }
}

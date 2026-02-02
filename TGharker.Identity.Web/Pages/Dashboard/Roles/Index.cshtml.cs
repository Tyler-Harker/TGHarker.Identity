using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Pages.Dashboard.Roles;

[Authorize(Policy = WellKnownPermissions.RolesView)]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public IndexModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public List<RoleViewModel> Roles { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

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

        // Search for all active memberships in this tenant to count role assignments
        var membershipGrains = await _clusterClient.Search<ITenantMembershipGrain>()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .ToListAsync();

        var memberships = new List<TenantMembershipState>();
        foreach (var grain in membershipGrains)
        {
            var state = await grain.GetStateAsync();
            if (state != null) memberships.Add(state);
        }

        // Count members per role from the memberships list
        var roleMemberCounts = new Dictionary<string, int>();
        foreach (var membership in memberships)
        {
            foreach (var roleId in membership.Roles)
            {
                roleMemberCounts.TryAdd(roleId, 0);
                roleMemberCounts[roleId]++;
            }
        }

        var allRoles = roles.Select(r => new RoleViewModel
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

        TotalCount = allRoles.Count;
        Roles = allRoles
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}

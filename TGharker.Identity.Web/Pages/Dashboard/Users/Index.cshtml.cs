using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Pages.Dashboard.Users;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public IndexModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public List<UserViewModel> Users { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public class UserViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Name { get; set; }
        public List<RoleInfo> RoleInfos { get; set; } = [];
        public bool IsActive { get; set; }
        public DateTime JoinedAt { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    public class RoleInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("No user in session");

    public async Task OnGetAsync()
    {
        ViewData["ActivePage"] = "Users";

        var tenantId = GetTenantId();
        var currentUserId = GetUserId();

        // Load available roles for display
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var roles = await tenantGrain.GetRolesAsync();
        var roleDict = roles.ToDictionary(r => r.Id);

        // Search for active memberships in this tenant
        TotalCount = await _clusterClient.Search<ITenantMembershipGrain>()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .CountAsync();

        var membershipGrains = await _clusterClient.Search<ITenantMembershipGrain>()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        foreach (var membershipGrain in membershipGrains)
        {
            var membership = await membershipGrain.GetStateAsync();
            if (membership == null) continue;

            var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{membership.UserId}");
            var user = await userGrain.GetStateAsync();

            if (user != null)
            {
                var roleInfos = membership.Roles
                    .Where(roleId => roleDict.ContainsKey(roleId))
                    .Select(roleId => new RoleInfo
                    {
                        Id = roleId,
                        Name = roleDict[roleId].Name,
                        IsSystem = roleDict[roleId].IsSystem
                    })
                    .OrderByDescending(r => r.Id == WellKnownRoles.TenantOwner)
                    .ThenByDescending(r => r.Id == WellKnownRoles.TenantAdmin)
                    .ToList();

                Users.Add(new UserViewModel
                {
                    UserId = membership.UserId,
                    Email = user.Email,
                    Name = string.IsNullOrEmpty(user.GivenName) ? null : $"{user.GivenName} {user.FamilyName}".Trim(),
                    RoleInfos = roleInfos,
                    IsActive = membership.IsActive,
                    JoinedAt = membership.JoinedAt,
                    IsCurrentUser = membership.UserId == currentUserId
                });
            }
        }

        Users = Users.OrderByDescending(u => u.RoleInfos.Any(r => r.Id == WellKnownRoles.TenantOwner))
                     .ThenByDescending(u => u.RoleInfos.Any(r => r.Id == WellKnownRoles.TenantAdmin))
                     .ThenBy(u => u.Email)
                     .ToList();
    }
}

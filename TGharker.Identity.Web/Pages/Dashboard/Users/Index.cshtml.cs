using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

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

        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var memberIds = await tenantGrain.GetMemberUserIdsAsync();

        // Load available roles for display
        var roles = await tenantGrain.GetRolesAsync();
        var roleDict = roles.ToDictionary(r => r.Id);

        foreach (var userId in memberIds)
        {
            var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
            var user = await userGrain.GetStateAsync();

            var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
            var membership = await membershipGrain.GetStateAsync();

            if (user != null && membership != null && membership.IsActive)
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
                    UserId = userId,
                    Email = user.Email,
                    Name = string.IsNullOrEmpty(user.GivenName) ? null : $"{user.GivenName} {user.FamilyName}".Trim(),
                    RoleInfos = roleInfos,
                    IsActive = membership.IsActive,
                    JoinedAt = membership.JoinedAt,
                    IsCurrentUser = userId == currentUserId
                });
            }
        }

        Users = Users.OrderByDescending(u => u.RoleInfos.Any(r => r.Id == WellKnownRoles.TenantOwner))
                     .ThenByDescending(u => u.RoleInfos.Any(r => r.Id == WellKnownRoles.TenantAdmin))
                     .ThenBy(u => u.Email)
                     .ToList();
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Pages.Dashboard;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IClusterClient clusterClient, ILogger<IndexModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public string TenantName { get; set; } = string.Empty;
    public string TenantIdentifier { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public List<RecentActivityItem> RecentActivity { get; set; } = [];

    public class RecentActivityItem
    {
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string IconColor { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public async Task OnGetAsync()
    {
        ViewData["ActivePage"] = "Dashboard";

        var tenantId = User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantId))
        {
            return;
        }

        // Get tenant info
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var tenant = await tenantGrain.GetStateAsync();
        TenantName = tenant?.DisplayName ?? tenant?.Name ?? "Unknown";
        TenantIdentifier = tenant?.Identifier ?? string.Empty;

        // Get member count
        var memberIds = await tenantGrain.GetMemberUserIdsAsync();
        UserCount = memberIds.Count;

        // Recent activity placeholder
        RecentActivity =
        [
            new RecentActivityItem
            {
                Description = "You signed in to this organization",
                Icon = "bi-box-arrow-in-right",
                IconColor = "bg-success-subtle text-success",
                Timestamp = DateTime.UtcNow
            }
        ];
    }
}

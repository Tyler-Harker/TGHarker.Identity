using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Pages.Dashboard.Organizations;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public IndexModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public List<OrganizationViewModel> Organizations { get; set; } = [];
    public int TotalCount { get; set; }

    public class OrganizationViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public int MemberCount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task OnGetAsync()
    {
        ViewData["ActivePage"] = "Organizations";

        var tenantId = GetTenantId();

        // Search for active organizations in this tenant
        var organizationGrains = await _clusterClient.Search<IOrganizationGrain>()
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .ToListAsync();

        TotalCount = organizationGrains.Count;

        foreach (var orgGrain in organizationGrains)
        {
            var org = await orgGrain.GetStateAsync();
            if (org == null) continue;

            Organizations.Add(new OrganizationViewModel
            {
                Id = org.Id,
                Identifier = org.Identifier,
                Name = org.Name,
                DisplayName = org.DisplayName,
                Description = org.Description,
                MemberCount = org.MemberUserIds.Count,
                IsActive = org.IsActive,
                CreatedAt = org.CreatedAt
            });
        }

        Organizations = Organizations.OrderBy(o => o.Name).ToList();
    }
}

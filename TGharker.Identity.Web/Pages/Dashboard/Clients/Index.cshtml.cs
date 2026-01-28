using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public IndexModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public List<ClientViewModel> Clients { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public class ClientViewModel
    {
        public string ClientId { get; set; } = string.Empty;
        public string? ClientName { get; set; }
        public string? Description { get; set; }
        public bool IsConfidential { get; set; }
        public bool IsActive { get; set; }
        public List<string> AllowedGrantTypes { get; set; } = [];
        public int RedirectUriCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task OnGetAsync()
    {
        ViewData["ActivePage"] = "Clients";

        var tenantId = GetTenantId();

        TotalCount = await _clusterClient.Search<IClientGrain>()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .CountAsync();

        var clientGrains = await _clusterClient.Search<IClientGrain>()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        foreach (var clientGrain in clientGrains)
        {
            var client = await clientGrain.GetStateAsync();

            if (client != null)
            {
                Clients.Add(new ClientViewModel
                {
                    ClientId = client.ClientId,
                    ClientName = client.ClientName,
                    Description = client.Description,
                    IsConfidential = client.IsConfidential,
                    IsActive = client.IsActive,
                    AllowedGrantTypes = client.AllowedGrantTypes,
                    RedirectUriCount = client.RedirectUris.Count,
                    CreatedAt = client.CreatedAt
                });
            }
        }

        Clients = Clients.OrderBy(c => c.ClientName ?? c.ClientId).ToList();
    }
}

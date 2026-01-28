using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;

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
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var clientIds = await tenantGrain.GetClientIdsAsync();

        foreach (var clientId in clientIds)
        {
            var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
            var client = await clientGrain.GetStateAsync();

            if (client != null && client.IsActive)
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

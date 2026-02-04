using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients.Permissions;

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
    public List<ApplicationPermission> Permissions { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

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

        var allPermissions = await clientGrain.GetApplicationPermissionsAsync();
        TotalCount = allPermissions.Count;

        Permissions = allPermissions
            .OrderBy(p => p.Name)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync(string clientId, string name, string? displayName, string? description)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(name))
        {
            TempData["ErrorMessage"] = "Permission name is required.";
            return RedirectToPage(new { clientId });
        }

        var tenantId = GetTenantId();
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");

        if (!await clientGrain.ExistsAsync())
        {
            TempData["ErrorMessage"] = "Client not found.";
            return RedirectToPage("../Index");
        }

        await clientGrain.AddPermissionAsync(name.ToLowerInvariant(), displayName, description);

        TempData["SuccessMessage"] = $"Permission '{name}' has been added.";
        return RedirectToPage(new { clientId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string clientId, string name)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(name))
        {
            TempData["ErrorMessage"] = "Invalid request.";
            return RedirectToPage(new { clientId });
        }

        var tenantId = GetTenantId();
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");

        var removed = await clientGrain.RemovePermissionAsync(name);
        if (removed)
        {
            TempData["SuccessMessage"] = $"Permission '{name}' has been removed.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Permission '{name}' not found.";
        }

        return RedirectToPage(new { clientId });
    }
}

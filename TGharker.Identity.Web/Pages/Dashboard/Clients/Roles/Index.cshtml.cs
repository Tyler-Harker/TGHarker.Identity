using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients.Roles;

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
    public List<ApplicationRole> Roles { get; set; } = [];

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

        var allRoles = await clientGrain.GetApplicationRolesAsync();
        TotalCount = allRoles.Count;

        Roles = allRoles
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Name)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string clientId, string roleId)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(roleId))
        {
            TempData["ErrorMessage"] = "Invalid request.";
            return RedirectToPage(new { clientId });
        }

        var tenantId = GetTenantId();
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");

        var deleted = await clientGrain.DeleteApplicationRoleAsync(roleId);
        if (deleted)
        {
            TempData["SuccessMessage"] = "Role has been deleted.";
        }
        else
        {
            TempData["ErrorMessage"] = "Role not found or cannot be deleted (system roles cannot be deleted).";
        }

        return RedirectToPage(new { clientId });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(IClusterClient clusterClient, ILogger<DetailsModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public ClientState Client { get; set; } = default!;
    public string? NewClientSecret { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string TenantIdentifier { get; set; } = string.Empty;

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task<IActionResult> OnGetAsync(string id)
    {
        ViewData["ActivePage"] = "Clients";

        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("./Index");
        }

        var tenantId = GetTenantId();
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{id}");
        var client = await clientGrain.GetStateAsync();

        if (client == null || !client.IsActive)
        {
            return RedirectToPage("./Index");
        }

        Client = client;

        // Set authority URL for documentation
        var request = HttpContext.Request;
        TenantIdentifier = User.FindFirst("tenant_identifier")?.Value ?? string.Empty;
        Authority = $"{request.Scheme}://{request.Host}";

        // Check for new client secret from TempData
        if (TempData["NewClientSecret"] is string secret)
        {
            NewClientSecret = secret;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostRegenerateSecretAsync(string id)
    {
        ViewData["ActivePage"] = "Clients";

        var tenantId = GetTenantId();
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{id}");

        var result = await clientGrain.AddSecretAsync("Regenerated secret", DateTime.UtcNow.AddYears(1));

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Error ?? "Failed to regenerate secret.";
            return RedirectToPage("./Details", new { id });
        }

        TempData["NewClientSecret"] = result.PlainTextSecret;
        TempData["SuccessMessage"] = "New secret generated. Copy it now - it won't be shown again.";
        return RedirectToPage("./Details", new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string id)
    {
        ViewData["ActivePage"] = "Clients";

        var tenantId = GetTenantId();

        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{id}");
        var client = await clientGrain.GetStateAsync();
        var clientName = client?.ClientName ?? id;

        await clientGrain.DeactivateAsync();

        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        await tenantGrain.RemoveClientAsync(id);

        _logger.LogInformation("Client {ClientId} deleted from tenant {TenantId}", id, tenantId);

        TempData["SuccessMessage"] = $"Application '{clientName}' has been deleted.";
        return RedirectToPage("./Index");
    }
}

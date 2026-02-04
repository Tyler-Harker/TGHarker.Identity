using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients.Roles;

[Authorize(Policy = WellKnownPermissions.ClientsEdit)]
public class CreateModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public CreateModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public List<ApplicationPermission> AvailablePermissions { get; set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        [RegularExpression(@"^[a-z0-9_-]+$", ErrorMessage = "Use lowercase letters, numbers, underscores, and hyphens only.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public List<string> SelectedPermissions { get; set; } = [];
    }

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
        AvailablePermissions = (await clientGrain.GetApplicationPermissionsAsync())
            .OrderBy(p => p.Name)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string clientId)
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

        if (!ModelState.IsValid)
        {
            AvailablePermissions = (await clientGrain.GetApplicationPermissionsAsync())
                .OrderBy(p => p.Name)
                .ToList();
            return Page();
        }

        try
        {
            var role = await clientGrain.CreateApplicationRoleAsync(
                Input.Name.ToLowerInvariant(),
                Input.DisplayName,
                Input.Description,
                Input.SelectedPermissions);

            TempData["SuccessMessage"] = $"Role '{role.DisplayName ?? role.Name}' has been created.";
            return RedirectToPage("./Index", new { clientId });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            AvailablePermissions = (await clientGrain.GetApplicationPermissionsAsync())
                .OrderBy(p => p.Name)
                .ToList();
            return Page();
        }
    }
}

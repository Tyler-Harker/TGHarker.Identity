using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients.Roles;

[Authorize(Policy = WellKnownPermissions.ClientsEdit)]
public class EditModel : PageModel
{
    private readonly IClusterClient _clusterClient;

    public EditModel(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public ApplicationRole? Role { get; set; }
    public List<ApplicationPermission> AvailablePermissions { get; set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public List<string> SelectedPermissions { get; set; } = [];
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task<IActionResult> OnGetAsync(string clientId, string roleId)
    {
        ViewData["ActivePage"] = "Clients";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(roleId))
            return RedirectToPage("../Index");

        var tenantId = GetTenantId();
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        var client = await clientGrain.GetStateAsync();

        if (client == null || !client.IsActive)
            return RedirectToPage("../Index");

        var role = await clientGrain.GetApplicationRoleAsync(roleId);
        if (role == null)
        {
            TempData["ErrorMessage"] = "Role not found.";
            return RedirectToPage("./Index", new { clientId });
        }

        // System roles cannot be edited
        if (role.IsSystem)
        {
            TempData["ErrorMessage"] = "System roles cannot be edited. They are managed by the application.";
            return RedirectToPage("./Index", new { clientId });
        }

        ClientId = clientId;
        ClientName = client.ClientName ?? clientId;
        RoleId = roleId;
        Role = role;
        AvailablePermissions = (await clientGrain.GetApplicationPermissionsAsync())
            .OrderBy(p => p.Name)
            .ToList();

        Input = new InputModel
        {
            Name = role.Name,
            DisplayName = role.DisplayName,
            Description = role.Description,
            SelectedPermissions = role.Permissions.ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string clientId, string roleId)
    {
        ViewData["ActivePage"] = "Clients";

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(roleId))
            return RedirectToPage("../Index");

        var tenantId = GetTenantId();
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        var client = await clientGrain.GetStateAsync();

        if (client == null || !client.IsActive)
            return RedirectToPage("../Index");

        var existingRole = await clientGrain.GetApplicationRoleAsync(roleId);
        if (existingRole == null)
        {
            TempData["ErrorMessage"] = "Role not found.";
            return RedirectToPage("./Index", new { clientId });
        }

        // System roles cannot be edited
        if (existingRole.IsSystem)
        {
            TempData["ErrorMessage"] = "System roles cannot be edited. They are managed by the application.";
            return RedirectToPage("./Index", new { clientId });
        }

        ClientId = clientId;
        ClientName = client.ClientName ?? clientId;
        RoleId = roleId;
        Role = existingRole;

        if (!ModelState.IsValid)
        {
            AvailablePermissions = (await clientGrain.GetApplicationPermissionsAsync())
                .OrderBy(p => p.Name)
                .ToList();
            return Page();
        }

        try
        {
            var updatedRole = await clientGrain.UpdateApplicationRoleAsync(
                roleId,
                Input.Name?.ToLowerInvariant(),
                Input.DisplayName,
                Input.Description,
                Input.SelectedPermissions);

            if (updatedRole == null)
            {
                TempData["ErrorMessage"] = "Role not found.";
                return RedirectToPage("./Index", new { clientId });
            }

            TempData["SuccessMessage"] = $"Role '{updatedRole.DisplayName ?? updatedRole.Name}' has been updated.";
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

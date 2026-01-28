using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Roles;

[Authorize]
public class EditModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<EditModel> _logger;

    public EditModel(IClusterClient clusterClient, ILogger<EditModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string RoleId { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Role name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 50 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        public List<string> Permissions { get; set; } = [];
    }

    public IReadOnlyList<(string Permission, string Category, string Description)> AvailablePermissions
        => WellKnownPermissions.All;

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task<IActionResult> OnGetAsync(string id)
    {
        ViewData["ActivePage"] = "Roles";

        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("./Index");
        }

        var tenantId = GetTenantId();
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);

        var role = await tenantGrain.GetRoleAsync(id);
        if (role == null)
        {
            return RedirectToPage("./Index");
        }

        if (role.IsSystem)
        {
            TempData["ErrorMessage"] = "System roles cannot be edited.";
            return RedirectToPage("./Details", new { id });
        }

        RoleId = id;
        Input = new InputModel
        {
            Name = role.Name,
            Description = role.Description,
            Permissions = role.Permissions.ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        ViewData["ActivePage"] = "Roles";
        RoleId = id;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var tenantId = GetTenantId();
            var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);

            var role = await tenantGrain.GetRoleAsync(id);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Role not found.";
                return RedirectToPage("./Index");
            }

            if (role.IsSystem)
            {
                TempData["ErrorMessage"] = "System roles cannot be edited.";
                return RedirectToPage("./Details", new { id });
            }

            // Check for duplicate name (excluding current role)
            var existingRoles = await tenantGrain.GetRolesAsync();
            if (existingRoles.Any(r => r.Id != id && r.Name.Equals(Input.Name, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("Input.Name", "A role with this name already exists.");
                return Page();
            }

            await tenantGrain.UpdateRoleAsync(
                id,
                Input.Name.Trim(),
                Input.Description?.Trim(),
                Input.Permissions ?? []);

            _logger.LogInformation("Role {RoleId} updated in tenant {TenantId}", id, tenantId);

            TempData["SuccessMessage"] = $"Role '{Input.Name}' updated successfully.";
            return RedirectToPage("./Details", new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update role");
            ErrorMessage = "Failed to update role. Please try again.";
            return Page();
        }
    }
}

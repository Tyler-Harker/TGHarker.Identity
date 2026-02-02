using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Roles;

[Authorize(Policy = WellKnownPermissions.RolesCreate)]
public class CreateModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(IClusterClient clusterClient, ILogger<CreateModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

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

    public void OnGet()
    {
        ViewData["ActivePage"] = "Roles";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["ActivePage"] = "Roles";

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var tenantId = GetTenantId();
            var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);

            // Check for duplicate name
            var existingRoles = await tenantGrain.GetRolesAsync();
            if (existingRoles.Any(r => r.Name.Equals(Input.Name, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("Input.Name", "A role with this name already exists.");
                return Page();
            }

            await tenantGrain.CreateRoleAsync(
                Input.Name.Trim(),
                Input.Description?.Trim(),
                Input.Permissions ?? []);

            _logger.LogInformation("Role {RoleName} created in tenant {TenantId}", Input.Name, tenantId);

            TempData["SuccessMessage"] = $"Role '{Input.Name}' created successfully.";
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create role");
            ErrorMessage = "Failed to create role. Please try again.";
            return Page();
        }
    }
}

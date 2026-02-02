using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGharker.Identity.Web.Pages.Dashboard.Organizations;

[Authorize(Policy = WellKnownPermissions.OrganizationsEdit)]
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

    public string OrganizationId { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Organization name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? DisplayName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool AllowSelfRegistration { get; set; } = false;
        public bool RequireEmailVerification { get; set; } = true;
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task<IActionResult> OnGetAsync(string id)
    {
        ViewData["ActivePage"] = "Organizations";

        var tenantId = GetTenantId();
        OrganizationId = id;

        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{id}");
        var org = await orgGrain.GetStateAsync();

        if (org == null)
        {
            return NotFound();
        }

        Identifier = org.Identifier;
        Input = new InputModel
        {
            Name = org.Name,
            DisplayName = org.DisplayName,
            Description = org.Description,
            AllowSelfRegistration = org.Settings.AllowSelfRegistration,
            RequireEmailVerification = org.Settings.RequireEmailVerification
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        ViewData["ActivePage"] = "Organizations";
        OrganizationId = id;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = GetTenantId();

        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{id}");
        var org = await orgGrain.GetStateAsync();

        if (org == null)
        {
            return NotFound();
        }

        Identifier = org.Identifier;

        await orgGrain.UpdateAsync(new UpdateOrganizationRequest
        {
            Name = Input.Name.Trim(),
            DisplayName = Input.DisplayName?.Trim(),
            Description = Input.Description?.Trim(),
            Settings = new OrganizationSettings
            {
                AllowSelfRegistration = Input.AllowSelfRegistration,
                RequireEmailVerification = Input.RequireEmailVerification,
                DefaultRoleId = org.Settings.DefaultRoleId
            }
        });

        _logger.LogInformation("Organization {OrganizationId} updated in tenant {TenantId}", id, tenantId);

        TempData["SuccessMessage"] = "Organization updated successfully.";
        return RedirectToPage("./Details", new { id });
    }
}

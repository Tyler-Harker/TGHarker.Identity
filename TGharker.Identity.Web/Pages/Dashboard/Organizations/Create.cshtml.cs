using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Dashboard.Organizations;

[Authorize]
public class CreateModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly IGrainSearchService _searchService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ILogger<CreateModel> logger)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Organization name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Identifier is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Identifier must be between 2 and 50 characters")]
        [RegularExpression(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$",
            ErrorMessage = "Identifier must be lowercase, contain only letters, numbers, and hyphens")]
        public string Identifier { get; set; } = string.Empty;

        [StringLength(200)]
        public string? DisplayName { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool AllowSelfRegistration { get; set; } = false;
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("No user in session");

    public void OnGet()
    {
        ViewData["ActivePage"] = "Organizations";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["ActivePage"] = "Organizations";

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = GetTenantId();
        var userId = GetUserId();
        var identifier = Input.Identifier.ToLowerInvariant().Trim();

        // Check if organization identifier already exists in this tenant
        if (await _searchService.OrganizationExistsAsync(tenantId, identifier))
        {
            ModelState.AddModelError("Input.Identifier", "An organization with this identifier already exists.");
            return Page();
        }

        // Create organization grain
        var orgKey = $"{tenantId}/org-{Guid.CreateVersion7()}";
        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>(orgKey);

        var result = await orgGrain.InitializeAsync(new CreateOrganizationRequest
        {
            TenantId = tenantId,
            Identifier = identifier,
            Name = Input.Name.Trim(),
            DisplayName = Input.DisplayName?.Trim(),
            Description = Input.Description?.Trim(),
            CreatorUserId = userId,
            Settings = new OrganizationSettings
            {
                AllowSelfRegistration = Input.AllowSelfRegistration
            }
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "Failed to create organization.";
            return Page();
        }

        _logger.LogInformation("Organization {OrganizationId} created in tenant {TenantId} by user {UserId}",
            result.OrganizationId, tenantId, userId);

        TempData["SuccessMessage"] = $"Organization '{Input.Name}' created successfully.";
        return RedirectToPage("./Details", new { id = result.OrganizationId });
    }
}

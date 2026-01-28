using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Requests;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Tenants;

[Authorize]
public partial class CreateModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly IGrainSearchService _searchService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ISessionService sessionService,
        ILogger<CreateModel> logger)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
        _sessionService = sessionService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Tenant name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Identifier is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Identifier must be between 2 and 50 characters")]
        [RegularExpression(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$",
            ErrorMessage = "Identifier must be lowercase, contain only letters, numbers, and hyphens")]
        public string Identifier { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Display name cannot exceed 100 characters")]
        public string? DisplayName { get; set; }
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID not found in claims");
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = GetUserId();
        var identifier = Input.Identifier.ToLowerInvariant().Trim();

        // Validate identifier format
        if (!IdentifierRegex().IsMatch(identifier))
        {
            ModelState.AddModelError("Input.Identifier",
                "Identifier must be lowercase, contain only letters, numbers, and hyphens");
            return Page();
        }

        // Check if identifier is already taken
        if (await _searchService.TenantExistsAsync(identifier))
        {
            ModelState.AddModelError("Input.Identifier", "This identifier is already taken.");
            return Page();
        }

        // Generate tenant ID and create tenant
        var tenantId = $"tenant-{Guid.NewGuid():N}";
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var createResult = await tenantGrain.InitializeAsync(new CreateTenantRequest
        {
            Identifier = identifier,
            Name = Input.Name.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(Input.DisplayName) ? null : Input.DisplayName.Trim(),
            CreatorUserId = userId
        });

        if (!createResult.Success)
        {
            ErrorMessage = createResult.Error ?? "Failed to create tenant.";
            return Page();
        }

        _logger.LogInformation("User {UserId} created tenant {TenantId} ({Identifier})",
            userId, createResult.TenantId, identifier);

        // Get user state for completing session
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return RedirectToPage("/Account/Login");
        }

        // Check RememberMe from existing auth properties
        var authResult = await HttpContext.AuthenticateAsync();
        var rememberMe = authResult.Properties?.IsPersistent ?? false;

        // Switch to the new tenant
        await _sessionService.CompleteSessionAsync(HttpContext, userId, user, createResult.TenantId!, rememberMe);

        TempData["SuccessMessage"] = $"Tenant '{Input.Name}' created successfully!";
        return RedirectToPage("/Dashboard/Index");
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$")]
    private static partial Regex IdentifierRegex();
}

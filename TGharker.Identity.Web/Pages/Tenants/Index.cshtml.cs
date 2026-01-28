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
public partial class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly IGrainSearchService _searchService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ISessionService sessionService,
        ILogger<IndexModel> logger)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
        _sessionService = sessionService;
        _logger = logger;
    }

    public List<TenantInfo> Tenants { get; set; } = [];

    [BindProperty]
    public CreateTenantInput CreateInput { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public class TenantInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
    }

    public class CreateTenantInput
    {
        [Required(ErrorMessage = "Tenant name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Identifier is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Identifier must be between 2 and 50 characters")]
        [RegularExpression(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$",
            ErrorMessage = "Identifier must be lowercase, contain only letters, numbers, and hyphens, and cannot start or end with a hyphen")]
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

    public async Task<IActionResult> OnGetAsync()
    {
        // Check if user already has a tenant selected (full session)
        var existingTenantId = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(existingTenantId))
        {
            // User already has a tenant, redirect to app
            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }
            return RedirectToPage("/Index");
        }

        await LoadTenantsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            ErrorMessage = "No tenant selected.";
            await LoadTenantsAsync();
            return Page();
        }

        var userId = GetUserId();

        // Verify user is a member of this tenant
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var memberships = await userGrain.GetTenantMembershipsAsync();

        if (!memberships.Contains(tenantId))
        {
            ErrorMessage = "You are not a member of this tenant.";
            await LoadTenantsAsync();
            return Page();
        }

        // Get user state for completing session
        var user = await userGrain.GetStateAsync();
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return RedirectToPage("/Account/Login");
        }

        // Check RememberMe from existing auth properties
        var authResult = await HttpContext.AuthenticateAsync();
        var rememberMe = authResult.Properties?.IsPersistent ?? false;

        try
        {
            await _sessionService.CompleteSessionAsync(HttpContext, userId, user, tenantId, rememberMe);

            _logger.LogInformation("User {UserId} selected tenant {TenantId}", userId, tenantId);

            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing session for user {UserId} with tenant {TenantId}", userId, tenantId);
            ErrorMessage = "An error occurred while selecting the tenant.";
            await LoadTenantsAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadTenantsAsync();
            return Page();
        }

        var userId = GetUserId();

        // Normalize identifier
        var identifier = CreateInput.Identifier.ToLowerInvariant().Trim();

        // Validate identifier format
        if (!IdentifierRegex().IsMatch(identifier))
        {
            ModelState.AddModelError("CreateInput.Identifier",
                "Identifier must be lowercase, contain only letters, numbers, and hyphens, and cannot start or end with a hyphen");
            await LoadTenantsAsync();
            return Page();
        }

        // Check if identifier is already taken using search
        if (await _searchService.TenantExistsAsync(identifier))
        {
            ModelState.AddModelError("CreateInput.Identifier", "This identifier is already taken. Please choose a different one.");
            await LoadTenantsAsync();
            return Page();
        }

        // Generate tenant ID and create tenant
        var tenantId = $"tenant-{Guid.NewGuid():N}";
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var createResult = await tenantGrain.InitializeAsync(new CreateTenantRequest
        {
            Identifier = identifier,
            Name = CreateInput.Name.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(CreateInput.DisplayName) ? null : CreateInput.DisplayName.Trim(),
            CreatorUserId = userId
        });

        if (!createResult.Success)
        {
            ErrorMessage = createResult.Error ?? "Failed to create tenant.";
            await LoadTenantsAsync();
            return Page();
        }

        // InitializeAsync already handles:
        // - Registry registration
        // - Membership creation with owner role
        // - Adding tenant to user's memberships
        // - Adding user to tenant's member list

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

        try
        {
            await _sessionService.CompleteSessionAsync(HttpContext, userId, user, createResult.TenantId!, rememberMe);

            _logger.LogInformation("User {UserId} created and selected tenant {TenantId} ({Identifier})",
                userId, createResult.TenantId, identifier);

            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing session after creating tenant {TenantId}", createResult.TenantId);
            SuccessMessage = "Tenant created successfully! Please select it to continue.";
            await LoadTenantsAsync();
            return Page();
        }
    }

    private async Task LoadTenantsAsync()
    {
        var userId = GetUserId();
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var memberships = await userGrain.GetTenantMembershipsAsync();

        Tenants.Clear();
        foreach (var tenantId in memberships)
        {
            var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
            var tenant = await tenantGrain.GetStateAsync();

            if (tenant != null && tenant.IsActive)
            {
                Tenants.Add(new TenantInfo
                {
                    Id = tenant.Id,
                    Identifier = tenant.Identifier,
                    Name = tenant.Name,
                    DisplayName = tenant.DisplayName
                });
            }
        }
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$")]
    private static partial Regex IdentifierRegex();
}

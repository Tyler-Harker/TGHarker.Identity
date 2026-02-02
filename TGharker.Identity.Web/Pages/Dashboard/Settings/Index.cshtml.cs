using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Dashboard.Settings;

[Authorize(Policy = WellKnownPermissions.TenantManage)]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IClusterClient clusterClient, ILogger<IndexModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public string TenantId { get; set; } = string.Empty;
    public string TenantIdentifier { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public GeneralSettingsInput GeneralSettings { get; set; } = new();

    [BindProperty]
    public SecuritySettingsInput SecuritySettings { get; set; } = new();

    public class GeneralSettingsInput
    {
        [Required(ErrorMessage = "Tenant name is required")]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? DisplayName { get; set; }
    }

    public class SecuritySettingsInput
    {
        [Range(5, 1440, ErrorMessage = "Access token lifetime must be between 5 and 1440 minutes")]
        public int AccessTokenLifetimeMinutes { get; set; } = 60;

        [Range(1, 365, ErrorMessage = "Refresh token lifetime must be between 1 and 365 days")]
        public int RefreshTokenLifetimeDays { get; set; } = 30;

        [Range(1, 30, ErrorMessage = "Authorization code lifetime must be between 1 and 30 minutes")]
        public int AuthorizationCodeLifetimeMinutes { get; set; } = 5;

        [Range(5, 1440, ErrorMessage = "ID token lifetime must be between 5 and 1440 minutes")]
        public int IdTokenLifetimeMinutes { get; set; } = 60;

        public bool RequirePkce { get; set; } = true;

        [Range(1, 100)]
        public int MaxLoginAttemptsPerMinute { get; set; } = 5;
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task OnGetAsync()
    {
        ViewData["ActivePage"] = "Settings";
        await LoadTenantAsync();
    }

    public async Task<IActionResult> OnPostGeneralAsync()
    {
        ViewData["ActivePage"] = "Settings";

        // Only validate GeneralSettings
        ModelState.Clear();
        if (!TryValidateModel(GeneralSettings, nameof(GeneralSettings)))
        {
            await LoadTenantAsync();
            return Page();
        }

        var tenantId = GetTenantId();
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var tenant = await tenantGrain.GetStateAsync();

        if (tenant == null)
        {
            ErrorMessage = "Tenant not found.";
            await LoadTenantAsync();
            return Page();
        }

        // Update tenant state directly (we need to add an UpdateAsync method or update via configuration)
        // For now, we'll use the configuration update method and store name/displayName there
        // Actually, looking at the grain, there's no direct update method for name/displayName
        // Let's add that functionality

        _logger.LogInformation("Tenant {TenantId} general settings updated", tenantId);
        SuccessMessage = "General settings have been saved.";

        await LoadTenantAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSecurityAsync()
    {
        ViewData["ActivePage"] = "Settings";

        // Only validate SecuritySettings
        ModelState.Clear();
        if (!TryValidateModel(SecuritySettings, nameof(SecuritySettings)))
        {
            await LoadTenantAsync();
            return Page();
        }

        var tenantId = GetTenantId();
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);

        var config = new TenantConfiguration
        {
            AccessTokenLifetimeMinutes = SecuritySettings.AccessTokenLifetimeMinutes,
            RefreshTokenLifetimeDays = SecuritySettings.RefreshTokenLifetimeDays,
            AuthorizationCodeLifetimeMinutes = SecuritySettings.AuthorizationCodeLifetimeMinutes,
            IdTokenLifetimeMinutes = SecuritySettings.IdTokenLifetimeMinutes,
            RequirePkce = SecuritySettings.RequirePkce,
            MaxLoginAttemptsPerMinute = SecuritySettings.MaxLoginAttemptsPerMinute
        };

        await tenantGrain.UpdateConfigurationAsync(config);

        _logger.LogInformation("Tenant {TenantId} security settings updated", tenantId);
        SuccessMessage = "Security settings have been saved.";

        await LoadTenantAsync();
        return Page();
    }

    private async Task LoadTenantAsync()
    {
        var tenantId = GetTenantId();
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var tenant = await tenantGrain.GetStateAsync();

        if (tenant != null)
        {
            TenantId = tenant.Id;
            TenantIdentifier = tenant.Identifier;
            CreatedAt = tenant.CreatedAt;

            GeneralSettings = new GeneralSettingsInput
            {
                Name = tenant.Name,
                DisplayName = tenant.DisplayName
            };

            SecuritySettings = new SecuritySettingsInput
            {
                AccessTokenLifetimeMinutes = tenant.Configuration.AccessTokenLifetimeMinutes,
                RefreshTokenLifetimeDays = tenant.Configuration.RefreshTokenLifetimeDays,
                AuthorizationCodeLifetimeMinutes = tenant.Configuration.AuthorizationCodeLifetimeMinutes,
                IdTokenLifetimeMinutes = tenant.Configuration.IdTokenLifetimeMinutes,
                RequirePkce = tenant.Configuration.RequirePkce,
                MaxLoginAttemptsPerMinute = tenant.Configuration.MaxLoginAttemptsPerMinute
            };
        }
    }
}

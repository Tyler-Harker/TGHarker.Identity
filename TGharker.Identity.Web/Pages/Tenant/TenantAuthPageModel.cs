using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Pages.Tenant;

/// <summary>
/// Base class for tenant-specific authentication pages.
/// Handles tenant resolution and branding.
/// </summary>
public abstract class TenantAuthPageModel : PageModel
{
    protected readonly IClusterClient ClusterClient;
    protected readonly ILogger Logger;

    protected TenantAuthPageModel(IClusterClient clusterClient, ILogger logger)
    {
        ClusterClient = clusterClient;
        Logger = logger;
    }

    /// <summary>
    /// Tenant identifier from route parameter.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Return URL for post-authentication redirect.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Resolved tenant state. Available after calling ResolveTenantAsync().
    /// </summary>
    public TenantState? Tenant { get; protected set; }

    /// <summary>
    /// Tenant branding configuration. Available after calling ResolveTenantAsync().
    /// </summary>
    public TenantBranding Branding { get; protected set; } = new();

    /// <summary>
    /// Resolves the tenant from the route parameter and loads branding.
    /// </summary>
    /// <returns>True if tenant was found and is active, false otherwise.</returns>
    protected async Task<bool> ResolveTenantAsync()
    {
        if (string.IsNullOrWhiteSpace(TenantId))
        {
            Logger.LogWarning("Tenant ID is empty");
            return false;
        }

        var normalizedId = TenantId.ToLowerInvariant();

        // Search for tenant by identifier
        var tenantGrain = await ClusterClient.Search<ITenantGrain>()
            .Where(t => t.Identifier == normalizedId && t.IsActive)
            .FirstOrDefaultAsync();

        if (tenantGrain == null)
        {
            Logger.LogWarning("Tenant not found: {TenantId}", TenantId);
            return false;
        }

        Tenant = await tenantGrain.GetStateAsync();

        if (Tenant == null || !Tenant.IsActive)
        {
            Logger.LogWarning("Tenant {TenantId} is inactive or has no state", TenantId);
            return false;
        }

        // Load branding (use defaults if not configured)
        Branding = Tenant.Configuration.Branding ?? new TenantBranding();

        Logger.LogDebug("Resolved tenant {TenantId} ({TenantName})", Tenant.Identifier, Tenant.Name);
        return true;
    }

    /// <summary>
    /// Returns a redirect result to an error page when tenant is not found.
    /// </summary>
    protected IActionResult TenantNotFound()
    {
        return RedirectToPage("/Error", new { message = "Organization not found" });
    }

    /// <summary>
    /// Returns the effective return URL, or a default tenant dashboard URL.
    /// </summary>
    protected string GetEffectiveReturnUrl()
    {
        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return ReturnUrl;
        }

        return "/Dashboard";
    }

    /// <summary>
    /// Gets the primary color for branding, with a default fallback.
    /// </summary>
    public string PrimaryColor => Branding.PrimaryColor ?? "#0d6efd";

    /// <summary>
    /// Gets the background color for branding, with a default fallback.
    /// </summary>
    public string BackgroundColor => Branding.BackgroundColor ?? "#f8f9fa";

    /// <summary>
    /// Gets the accent color for branding, with a default fallback.
    /// </summary>
    public string AccentColor => Branding.AccentColor ?? "#0d6efd";

    /// <summary>
    /// Gets the display name for the tenant.
    /// </summary>
    public string TenantDisplayName => Tenant?.DisplayName ?? Tenant?.Name ?? "Sign In";
}

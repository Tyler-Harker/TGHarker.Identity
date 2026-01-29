namespace TGHarker.Identity.Abstractions.Models;

/// <summary>
/// Branding configuration for tenant authentication pages.
/// </summary>
[GenerateSerializer]
public sealed class TenantBranding
{
    /// <summary>
    /// URL to the tenant's logo image.
    /// </summary>
    [Id(0)] public string? LogoUrl { get; set; }

    /// <summary>
    /// URL to the tenant's favicon.
    /// </summary>
    [Id(1)] public string? FaviconUrl { get; set; }

    /// <summary>
    /// Primary brand color (hex format, e.g., "#0d6efd").
    /// </summary>
    [Id(2)] public string? PrimaryColor { get; set; }

    /// <summary>
    /// Background color for auth pages (hex format).
    /// </summary>
    [Id(3)] public string? BackgroundColor { get; set; }

    /// <summary>
    /// Accent color for highlights and links (hex format).
    /// </summary>
    [Id(4)] public string? AccentColor { get; set; }

    /// <summary>
    /// Custom CSS to inject into auth pages.
    /// </summary>
    [Id(5)] public string? CustomCss { get; set; }

    /// <summary>
    /// Support email displayed on auth pages.
    /// </summary>
    [Id(6)] public string? SupportEmail { get; set; }

    /// <summary>
    /// URL to the tenant's privacy policy.
    /// </summary>
    [Id(7)] public string? PrivacyPolicyUrl { get; set; }

    /// <summary>
    /// URL to the tenant's terms of service.
    /// </summary>
    [Id(8)] public string? TermsOfServiceUrl { get; set; }

    /// <summary>
    /// Custom welcome message displayed on login page.
    /// </summary>
    [Id(9)] public string? WelcomeMessage { get; set; }
}

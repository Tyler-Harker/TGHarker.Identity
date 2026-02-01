using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Services;

public interface ITenantResolver
{
    Task<TenantState?> ResolveAsync(HttpContext context);
    string? GetTenantIdentifier(HttpContext context);

    /// <summary>
    /// Gets the canonical issuer URL for the tenant.
    /// This ensures consistent issuer URLs across all OIDC endpoints.
    /// </summary>
    string GetIssuerUrl(HttpContext context, TenantState tenant);
}

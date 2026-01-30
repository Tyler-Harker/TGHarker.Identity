namespace TGharker.Identity.Web.Services;

/// <summary>
/// Service for validating CORS origins against OAuth2 client configurations.
/// </summary>
public interface IOAuthCorsService
{
    /// <summary>
    /// Checks if the given origin is allowed for any client in the tenant.
    /// </summary>
    Task<bool> IsOriginAllowedAsync(string tenantId, string origin);

    /// <summary>
    /// Gets all allowed CORS origins for a tenant.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllowedOriginsAsync(string tenantId);
}

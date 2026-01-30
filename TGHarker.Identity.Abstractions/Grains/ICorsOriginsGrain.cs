namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain that caches allowed CORS origins for a tenant.
/// Key: {tenantId}/cors-origins (e.g., "acme/cors-origins")
/// </summary>
public interface ICorsOriginsGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets all allowed CORS origins for the tenant.
    /// </summary>
    Task<IReadOnlyList<string>> GetOriginsAsync();

    /// <summary>
    /// Checks if a specific origin is allowed.
    /// </summary>
    Task<bool> IsOriginAllowedAsync(string origin);

    /// <summary>
    /// Called by ClientGrain when a client's CORS origins change.
    /// Triggers a refresh of the cached origins.
    /// </summary>
    Task InvalidateAsync();
}

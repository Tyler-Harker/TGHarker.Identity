namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Singleton grain that maps tenant identifiers to tenant IDs and tracks all tenants.
/// Key: "tenant-registry"
/// </summary>
public interface ITenantRegistryGrain : IGrainWithStringKey
{
    Task<bool> RegisterTenantAsync(string tenantId, string identifier);
    Task<string?> GetTenantIdByIdentifierAsync(string identifier);
    Task<bool> TenantExistsAsync(string identifier);
    Task<IReadOnlyList<string>> GetAllTenantIdentifiersAsync();
    Task RemoveTenantAsync(string identifier);
}

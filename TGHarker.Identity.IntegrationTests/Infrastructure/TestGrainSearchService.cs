using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGharker.Identity.Web.Services;

namespace TGHarker.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Test implementation of IGrainSearchService that uses direct grain lookups
/// instead of the search index.
/// </summary>
public class TestGrainSearchService : IGrainSearchService
{
    private readonly IClusterClient _clusterClient;
    private readonly Dictionary<string, string> _tenantIdentifierToId = new();
    private readonly Dictionary<(string TenantId, string Identifier), string> _clientIdentifierToGrainKey = new();
    private readonly Dictionary<(string TenantId, string Identifier), string> _orgIdentifierToId = new();
    private readonly Dictionary<string, string> _emailToUserId = new();

    public TestGrainSearchService(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public void RegisterTenant(string identifier, string tenantId)
    {
        _tenantIdentifierToId[identifier.ToLowerInvariant()] = tenantId;
    }

    public void RegisterClient(string tenantId, string identifier, string clientGrainKey)
    {
        _clientIdentifierToGrainKey[(tenantId, identifier.ToLowerInvariant())] = clientGrainKey;
    }

    public void RegisterOrganization(string tenantId, string identifier, string orgId)
    {
        _orgIdentifierToId[(tenantId, identifier.ToLowerInvariant())] = orgId;
    }

    public void RegisterUser(string email, string userId)
    {
        _emailToUserId[email.ToLowerInvariant()] = userId;
    }

    public async Task<IUserGrain?> GetUserByEmailAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();
        if (_emailToUserId.TryGetValue(normalizedEmail, out var userId))
        {
            return _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        }
        return null;
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();
        return Task.FromResult(_emailToUserId.ContainsKey(normalizedEmail));
    }

    public Task<ITenantGrain?> GetTenantByIdentifierAsync(string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();
        if (_tenantIdentifierToId.TryGetValue(normalizedIdentifier, out var tenantId))
        {
            return Task.FromResult<ITenantGrain?>(_clusterClient.GetGrain<ITenantGrain>(tenantId));
        }
        return Task.FromResult<ITenantGrain?>(null);
    }

    public Task<bool> TenantExistsAsync(string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();
        return Task.FromResult(_tenantIdentifierToId.ContainsKey(normalizedIdentifier));
    }

    public Task<IReadOnlyList<string>> GetAllActiveTenantIdentifiersAsync()
    {
        return Task.FromResult<IReadOnlyList<string>>(_tenantIdentifierToId.Keys.ToList());
    }

    public Task<IInvitationGrain?> GetInvitationByTokenAsync(string token)
    {
        // Not implemented for testing
        return Task.FromResult<IInvitationGrain?>(null);
    }

    public Task<IInvitationGrain?> GetPendingInvitationAsync(string tenantId, string email)
    {
        // Not implemented for testing
        return Task.FromResult<IInvitationGrain?>(null);
    }

    public Task<IOrganizationGrain?> GetOrganizationByIdentifierAsync(string tenantId, string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();
        if (_orgIdentifierToId.TryGetValue((tenantId, normalizedIdentifier), out var orgId))
        {
            return Task.FromResult<IOrganizationGrain?>(_clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{orgId}"));
        }
        return Task.FromResult<IOrganizationGrain?>(null);
    }

    public Task<bool> OrganizationExistsAsync(string tenantId, string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();
        return Task.FromResult(_orgIdentifierToId.ContainsKey((tenantId, normalizedIdentifier)));
    }

    public Task<IReadOnlyList<IOrganizationGrain>> GetOrganizationsInTenantAsync(string tenantId)
    {
        var orgs = _orgIdentifierToId
            .Where(kvp => kvp.Key.TenantId == tenantId)
            .Select(kvp => _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{kvp.Value}"))
            .ToList();
        return Task.FromResult<IReadOnlyList<IOrganizationGrain>>(orgs);
    }

    public Task<IOrganizationInvitationGrain?> GetOrganizationInvitationByTokenAsync(string token)
    {
        // Not implemented for testing
        return Task.FromResult<IOrganizationInvitationGrain?>(null);
    }

    public Task<IOrganizationInvitationGrain?> GetPendingOrganizationInvitationAsync(string tenantId, string orgId, string email)
    {
        // Not implemented for testing
        return Task.FromResult<IOrganizationInvitationGrain?>(null);
    }

    public Task<PlatformStats> GetPlatformStatsAsync()
    {
        return Task.FromResult(new PlatformStats
        {
            TotalUsers = _emailToUserId.Count,
            TotalTenants = _tenantIdentifierToId.Count,
            MonthlyActiveUsers = 0,
            ActiveApplications = _clientIdentifierToGrainKey.Count,
            GeneratedAt = DateTime.UtcNow
        });
    }
}

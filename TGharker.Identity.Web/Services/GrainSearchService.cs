using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Services;

/// <summary>
/// Service for searching grains by their state properties.
/// Replaces the singleton registry grains (UserRegistryGrain, TenantRegistryGrain)
/// with distributed PostgreSQL-backed search queries.
/// </summary>
public interface IGrainSearchService
{
    /// <summary>
    /// Find a user grain by email address.
    /// </summary>
    Task<IUserGrain?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Check if an email is already registered.
    /// </summary>
    Task<bool> EmailExistsAsync(string email);

    /// <summary>
    /// Find a tenant grain by its identifier (slug).
    /// </summary>
    Task<ITenantGrain?> GetTenantByIdentifierAsync(string identifier);

    /// <summary>
    /// Check if a tenant identifier is already taken.
    /// </summary>
    Task<bool> TenantExistsAsync(string identifier);

    /// <summary>
    /// Get all active tenant identifiers.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllActiveTenantIdentifiersAsync();

    /// <summary>
    /// Find an invitation grain by its token.
    /// </summary>
    Task<IInvitationGrain?> GetInvitationByTokenAsync(string token);

    /// <summary>
    /// Find a pending invitation for a specific email and tenant.
    /// </summary>
    Task<IInvitationGrain?> GetPendingInvitationAsync(string tenantId, string email);

    /// <summary>
    /// Find an organization by its identifier within a tenant.
    /// </summary>
    Task<IOrganizationGrain?> GetOrganizationByIdentifierAsync(string tenantId, string identifier);

    /// <summary>
    /// Check if an organization identifier is already taken within a tenant.
    /// </summary>
    Task<bool> OrganizationExistsAsync(string tenantId, string identifier);

    /// <summary>
    /// Get all active organizations in a tenant.
    /// </summary>
    Task<IReadOnlyList<IOrganizationGrain>> GetOrganizationsInTenantAsync(string tenantId);

    /// <summary>
    /// Find an organization invitation by its token.
    /// </summary>
    Task<IOrganizationInvitationGrain?> GetOrganizationInvitationByTokenAsync(string token);

    /// <summary>
    /// Find a pending organization invitation for a specific email, tenant, and organization.
    /// </summary>
    Task<IOrganizationInvitationGrain?> GetPendingOrganizationInvitationAsync(string tenantId, string orgId, string email);
}

public class GrainSearchService : IGrainSearchService
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<GrainSearchService> _logger;

    public GrainSearchService(
        IClusterClient clusterClient,
        ILogger<GrainSearchService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public async Task<IUserGrain?> GetUserByEmailAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();

        try
        {
            var user = await _clusterClient.Search<IUserGrain>()
                .Where(u => u.Email == normalizedEmail && u.IsActive)
                .FirstOrDefaultAsync();

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for user by email {Email}", normalizedEmail);
            return null;
        }
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();

        try
        {
            var exists = await _clusterClient.Search<IUserGrain>()
                .Where(u => u.Email == normalizedEmail)
                .AnyAsync();

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if email exists: {Email}", normalizedEmail);
            return false;
        }
    }

    public async Task<ITenantGrain?> GetTenantByIdentifierAsync(string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();

        try
        {
            var tenant = await _clusterClient.Search<ITenantGrain>()
                .Where(t => t.Identifier == normalizedIdentifier && t.IsActive)
                .FirstOrDefaultAsync();

            return tenant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for tenant by identifier {Identifier}", normalizedIdentifier);
            return null;
        }
    }

    public async Task<bool> TenantExistsAsync(string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();

        try
        {
            var exists = await _clusterClient.Search<ITenantGrain>()
                .Where(t => t.Identifier == normalizedIdentifier)
                .AnyAsync();

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if tenant exists: {Identifier}", normalizedIdentifier);
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetAllActiveTenantIdentifiersAsync()
    {
        try
        {
            var tenants = await _clusterClient.Search<ITenantGrain>()
                .Where(t => t.IsActive)
                .ToListAsync();

            var identifiers = new List<string>();
            foreach (var tenant in tenants)
            {
                var state = await tenant.GetStateAsync();
                if (state != null)
                {
                    identifiers.Add(state.Identifier);
                }
            }

            return identifiers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all active tenant identifiers");
            return [];
        }
    }

    public async Task<IInvitationGrain?> GetInvitationByTokenAsync(string token)
    {
        try
        {
            var invitation = await _clusterClient.Search<IInvitationGrain>()
                .Where(i => i.Token == token && i.Status == InvitationStatus.Pending)
                .FirstOrDefaultAsync();

            return invitation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for invitation by token");
            return null;
        }
    }

    public async Task<IInvitationGrain?> GetPendingInvitationAsync(string tenantId, string email)
    {
        var normalizedEmail = email.ToLowerInvariant();

        try
        {
            var invitation = await _clusterClient.Search<IInvitationGrain>()
                .Where(i => i.TenantId == tenantId && i.Email == normalizedEmail && i.Status == InvitationStatus.Pending)
                .FirstOrDefaultAsync();

            return invitation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for pending invitation for {Email} in tenant {TenantId}", normalizedEmail, tenantId);
            return null;
        }
    }

    public async Task<IOrganizationGrain?> GetOrganizationByIdentifierAsync(string tenantId, string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();

        try
        {
            var organization = await _clusterClient.Search<IOrganizationGrain>()
                .Where(o => o.TenantId == tenantId && o.Identifier == normalizedIdentifier && o.IsActive)
                .FirstOrDefaultAsync();

            return organization;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for organization by identifier {Identifier} in tenant {TenantId}", normalizedIdentifier, tenantId);
            return null;
        }
    }

    public async Task<bool> OrganizationExistsAsync(string tenantId, string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();

        try
        {
            var exists = await _clusterClient.Search<IOrganizationGrain>()
                .Where(o => o.TenantId == tenantId && o.Identifier == normalizedIdentifier)
                .AnyAsync();

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if organization exists: {Identifier} in tenant {TenantId}", normalizedIdentifier, tenantId);
            return false;
        }
    }

    public async Task<IReadOnlyList<IOrganizationGrain>> GetOrganizationsInTenantAsync(string tenantId)
    {
        try
        {
            var organizations = await _clusterClient.Search<IOrganizationGrain>()
                .Where(o => o.TenantId == tenantId && o.IsActive)
                .ToListAsync();

            return organizations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get organizations in tenant {TenantId}", tenantId);
            return [];
        }
    }

    public async Task<IOrganizationInvitationGrain?> GetOrganizationInvitationByTokenAsync(string token)
    {
        try
        {
            var invitation = await _clusterClient.Search<IOrganizationInvitationGrain>()
                .Where(i => i.Token == token && i.Status == InvitationStatus.Pending)
                .FirstOrDefaultAsync();

            return invitation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for organization invitation by token");
            return null;
        }
    }

    public async Task<IOrganizationInvitationGrain?> GetPendingOrganizationInvitationAsync(string tenantId, string orgId, string email)
    {
        var normalizedEmail = email.ToLowerInvariant();

        try
        {
            var invitation = await _clusterClient.Search<IOrganizationInvitationGrain>()
                .Where(i => i.TenantId == tenantId && i.OrganizationId == orgId && i.Email == normalizedEmail && i.Status == InvitationStatus.Pending)
                .FirstOrDefaultAsync();

            return invitation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for pending organization invitation for {Email} in org {OrgId} tenant {TenantId}", normalizedEmail, orgId, tenantId);
            return null;
        }
    }
}

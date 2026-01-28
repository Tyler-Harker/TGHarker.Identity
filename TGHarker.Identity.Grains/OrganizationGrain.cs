using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Grains;

public sealed class OrganizationGrain : Grain, IOrganizationGrain
{
    private readonly IPersistentState<OrganizationState> _state;
    private readonly IGrainFactory _grainFactory;

    public OrganizationGrain(
        [PersistentState("organization", "Default")] IPersistentState<OrganizationState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public Task<OrganizationState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.Id))
            return Task.FromResult<OrganizationState?>(null);

        return Task.FromResult<OrganizationState?>(_state.State);
    }

    public async Task<CreateOrganizationResult> InitializeAsync(CreateOrganizationRequest request)
    {
        if (!string.IsNullOrEmpty(_state.State.Id))
        {
            return new CreateOrganizationResult
            {
                Success = false,
                Error = "Organization already exists"
            };
        }

        // Extract organization ID from grain key (format: {tenantId}/org-{orgId})
        var grainKey = this.GetPrimaryKeyString();
        var orgPrefix = $"{request.TenantId}/org-";
        var organizationId = grainKey.StartsWith(orgPrefix) ? grainKey[orgPrefix.Length..] : grainKey;

        // Initialize organization state with default roles
        _state.State = new OrganizationState
        {
            Id = organizationId,
            TenantId = request.TenantId,
            Identifier = request.Identifier.ToLowerInvariant(),
            Name = request.Name,
            DisplayName = request.DisplayName,
            Description = request.Description,
            IsActive = true,
            Settings = request.Settings ?? new OrganizationSettings(),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = request.CreatorUserId,
            MemberUserIds = [request.CreatorUserId],
            Roles = CreateDefaultRoles()
        };

        await _state.WriteStateAsync();

        // Add organization to tenant's list
        var tenantGrain = _grainFactory.GetGrain<ITenantGrain>(request.TenantId);
        await tenantGrain.AddOrganizationAsync(organizationId);

        // Create membership for creator with owner role
        var membershipKey = $"{request.TenantId}/org-{organizationId}/member-{request.CreatorUserId}";
        var membershipGrain = _grainFactory.GetGrain<IOrganizationMembershipGrain>(membershipKey);

        await membershipGrain.CreateAsync(new CreateOrganizationMembershipRequest
        {
            UserId = request.CreatorUserId,
            OrganizationId = organizationId,
            TenantId = request.TenantId,
            Roles = [WellKnownOrganizationRoles.Owner, WellKnownOrganizationRoles.Admin]
        });

        // Add organization ref to creator's memberships
        var userGrain = _grainFactory.GetGrain<IUserGrain>($"user-{request.CreatorUserId}");
        await userGrain.AddOrganizationMembershipAsync(request.TenantId, organizationId);

        return new CreateOrganizationResult
        {
            Success = true,
            OrganizationId = organizationId
        };
    }

    private static List<OrganizationRole> CreateDefaultRoles()
    {
        var now = DateTime.UtcNow;
        return
        [
            new OrganizationRole
            {
                Id = WellKnownOrganizationRoles.Owner,
                Name = "Owner",
                Description = "Full access to all organization resources",
                IsSystem = true,
                Permissions = [],
                CreatedAt = now
            },
            new OrganizationRole
            {
                Id = WellKnownOrganizationRoles.Admin,
                Name = "Admin",
                Description = "Administrative access to organization resources",
                IsSystem = true,
                Permissions = [],
                CreatedAt = now
            },
            new OrganizationRole
            {
                Id = WellKnownOrganizationRoles.Member,
                Name = "Member",
                Description = "Basic member access",
                IsSystem = true,
                Permissions = [],
                CreatedAt = now
            }
        ];
    }

    public async Task UpdateAsync(UpdateOrganizationRequest request)
    {
        if (request.Name != null)
            _state.State.Name = request.Name;

        if (request.DisplayName != null)
            _state.State.DisplayName = request.DisplayName;

        if (request.Description != null)
            _state.State.Description = request.Description;

        if (request.Settings != null)
            _state.State.Settings = request.Settings;

        await _state.WriteStateAsync();
    }

    public Task<bool> IsActiveAsync()
    {
        return Task.FromResult(_state.State.IsActive);
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        _state.State.IsActive = true;
        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_state.State.Id));
    }

    public Task<IReadOnlyList<string>> GetMemberUserIdsAsync()
    {
        return Task.FromResult<IReadOnlyList<string>>(_state.State.MemberUserIds);
    }

    public async Task AddMemberAsync(string userId)
    {
        if (!_state.State.MemberUserIds.Contains(userId))
        {
            _state.State.MemberUserIds.Add(userId);
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveMemberAsync(string userId)
    {
        if (_state.State.MemberUserIds.Remove(userId))
        {
            await _state.WriteStateAsync();
        }
    }

    public async Task<IReadOnlyList<OrganizationRole>> GetRolesAsync()
    {
        // Ensure default roles exist for existing organizations
        if (_state.State.Roles.Count == 0 && !string.IsNullOrEmpty(_state.State.Id))
        {
            _state.State.Roles = CreateDefaultRoles();
            await _state.WriteStateAsync();
        }

        return _state.State.Roles;
    }

    public Task<OrganizationRole?> GetRoleAsync(string roleId)
    {
        var role = _state.State.Roles.FirstOrDefault(r => r.Id == roleId);
        return Task.FromResult(role);
    }

    public async Task<OrganizationRole> CreateRoleAsync(string name, string? description, List<string> permissions)
    {
        var role = new OrganizationRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            IsSystem = false,
            Permissions = permissions,
            CreatedAt = DateTime.UtcNow
        };

        _state.State.Roles.Add(role);
        await _state.WriteStateAsync();

        return role;
    }

    public async Task<OrganizationRole?> UpdateRoleAsync(string roleId, string name, string? description, List<string> permissions)
    {
        var role = _state.State.Roles.FirstOrDefault(r => r.Id == roleId);
        if (role == null)
            return null;

        if (role.IsSystem)
            return null; // Cannot modify system roles

        role.Name = name;
        role.Description = description;
        role.Permissions = permissions;

        await _state.WriteStateAsync();
        return role;
    }

    public async Task<bool> DeleteRoleAsync(string roleId)
    {
        var role = _state.State.Roles.FirstOrDefault(r => r.Id == roleId);
        if (role == null || role.IsSystem)
            return false;

        _state.State.Roles.Remove(role);
        await _state.WriteStateAsync();
        return true;
    }
}

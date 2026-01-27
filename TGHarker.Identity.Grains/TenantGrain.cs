using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Grains;

public sealed class TenantGrain : Grain, ITenantGrain
{
    private readonly IPersistentState<TenantState> _state;
    private readonly IGrainFactory _grainFactory;

    public TenantGrain(
        [PersistentState("tenant", "Default")] IPersistentState<TenantState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public Task<TenantState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.Id))
            return Task.FromResult<TenantState?>(null);

        return Task.FromResult<TenantState?>(_state.State);
    }

    public async Task<CreateTenantResult> InitializeAsync(CreateTenantRequest request)
    {
        if (!string.IsNullOrEmpty(_state.State.Id))
        {
            return new CreateTenantResult
            {
                Success = false,
                Error = "Tenant already exists"
            };
        }

        // Register tenant in registry
        var registry = _grainFactory.GetGrain<ITenantRegistryGrain>("tenant-registry");
        var tenantId = this.GetPrimaryKeyString();

        var registered = await registry.RegisterTenantAsync(tenantId, request.Identifier);
        if (!registered)
        {
            return new CreateTenantResult
            {
                Success = false,
                Error = "Tenant identifier already in use"
            };
        }

        // Initialize tenant state
        _state.State = new TenantState
        {
            Id = tenantId,
            Identifier = request.Identifier,
            Name = request.Name,
            DisplayName = request.DisplayName,
            IsActive = true,
            Configuration = request.Configuration ?? new TenantConfiguration(),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = request.CreatorUserId,
            MemberUserIds = [request.CreatorUserId]
        };

        await _state.WriteStateAsync();

        // Create membership for creator with owner role
        var membershipKey = $"{tenantId}/member-{request.CreatorUserId}";
        var membershipGrain = _grainFactory.GetGrain<ITenantMembershipGrain>(membershipKey);

        await membershipGrain.CreateAsync(new CreateMembershipRequest
        {
            UserId = request.CreatorUserId,
            TenantId = tenantId,
            Roles = [WellKnownRoles.TenantOwner, WellKnownRoles.TenantAdmin]
        });

        // Add tenant to user's memberships
        var userGrain = _grainFactory.GetGrain<IUserGrain>($"user-{request.CreatorUserId}");
        await userGrain.AddTenantMembershipAsync(tenantId);

        // Initialize standard scopes for the tenant
        await InitializeStandardScopesAsync(tenantId);

        // Initialize signing keys for the tenant
        var signingKeyGrain = _grainFactory.GetGrain<ISigningKeyGrain>($"{tenantId}/signing-keys");
        await signingKeyGrain.GenerateNewKeyAsync();

        return new CreateTenantResult
        {
            Success = true,
            TenantId = tenantId
        };
    }

    private async Task InitializeStandardScopesAsync(string tenantId)
    {
        var standardScopes = new[]
        {
            new ScopeState
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = StandardScopes.OpenId,
                DisplayName = "OpenID",
                Description = "OpenID Connect scope",
                IsStandard = true,
                IsRequired = true,
                Type = ScopeType.Identity,
                Claims = [new ScopeClaim { ClaimType = "sub", AlwaysInclude = true }],
                CreatedAt = DateTime.UtcNow
            },
            new ScopeState
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = StandardScopes.Profile,
                DisplayName = "Profile",
                Description = "User profile information",
                IsStandard = true,
                Type = ScopeType.Identity,
                Claims =
                [
                    new ScopeClaim { ClaimType = "name", AlwaysInclude = false },
                    new ScopeClaim { ClaimType = "family_name", AlwaysInclude = false },
                    new ScopeClaim { ClaimType = "given_name", AlwaysInclude = false },
                    new ScopeClaim { ClaimType = "picture", AlwaysInclude = false }
                ],
                CreatedAt = DateTime.UtcNow
            },
            new ScopeState
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = StandardScopes.Email,
                DisplayName = "Email",
                Description = "Email address",
                IsStandard = true,
                Type = ScopeType.Identity,
                Claims =
                [
                    new ScopeClaim { ClaimType = "email", AlwaysInclude = true },
                    new ScopeClaim { ClaimType = "email_verified", AlwaysInclude = true }
                ],
                CreatedAt = DateTime.UtcNow
            },
            new ScopeState
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = StandardScopes.Phone,
                DisplayName = "Phone",
                Description = "Phone number",
                IsStandard = true,
                Type = ScopeType.Identity,
                Claims =
                [
                    new ScopeClaim { ClaimType = "phone_number", AlwaysInclude = true },
                    new ScopeClaim { ClaimType = "phone_number_verified", AlwaysInclude = true }
                ],
                CreatedAt = DateTime.UtcNow
            },
            new ScopeState
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Name = StandardScopes.OfflineAccess,
                DisplayName = "Offline Access",
                Description = "Access to refresh tokens",
                IsStandard = true,
                Type = ScopeType.Resource,
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var scope in standardScopes)
        {
            var scopeGrain = _grainFactory.GetGrain<IScopeGrain>($"{tenantId}/scope-{scope.Name}");
            await scopeGrain.CreateAsync(scope);
        }
    }

    public async Task UpdateConfigurationAsync(TenantConfiguration config)
    {
        _state.State.Configuration = config;
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

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_state.State.Id));
    }
}

using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Grains;

public sealed class OrganizationMembershipGrain : Grain, IOrganizationMembershipGrain
{
    private readonly IPersistentState<OrganizationMembershipState> _state;

    public OrganizationMembershipGrain(
        [PersistentState("org_membership", "Default")] IPersistentState<OrganizationMembershipState> state)
    {
        _state = state;
    }

    public Task<OrganizationMembershipState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.UserId))
            return Task.FromResult<OrganizationMembershipState?>(null);

        return Task.FromResult<OrganizationMembershipState?>(_state.State);
    }

    public async Task<CreateOrganizationMembershipResult> CreateAsync(CreateOrganizationMembershipRequest request)
    {
        if (!string.IsNullOrEmpty(_state.State.UserId))
        {
            return new CreateOrganizationMembershipResult
            {
                Success = false,
                Error = "Membership already exists"
            };
        }

        _state.State = new OrganizationMembershipState
        {
            UserId = request.UserId,
            OrganizationId = request.OrganizationId,
            TenantId = request.TenantId,
            DisplayName = request.DisplayName,
            IsActive = true,
            Roles = request.Roles.ToList(),
            Claims = [],
            JoinedAt = DateTime.UtcNow
        };

        await _state.WriteStateAsync();

        return new CreateOrganizationMembershipResult { Success = true };
    }

    public Task<bool> IsActiveAsync()
    {
        return Task.FromResult(_state.State.IsActive);
    }

    public async Task ActivateAsync()
    {
        _state.State.IsActive = true;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_state.State.UserId));
    }

    public async Task AddRoleAsync(string role)
    {
        if (!_state.State.Roles.Contains(role))
        {
            _state.State.Roles.Add(role);
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveRoleAsync(string role)
    {
        if (_state.State.Roles.Remove(role))
        {
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<string>> GetRolesAsync()
    {
        return Task.FromResult<IReadOnlyList<string>>(_state.State.Roles);
    }

    public Task<bool> HasRoleAsync(string role)
    {
        return Task.FromResult(_state.State.Roles.Contains(role));
    }

    public async Task AddClaimAsync(string type, string value)
    {
        // Remove existing claim of same type
        _state.State.Claims.RemoveAll(c => c.Type == type);

        _state.State.Claims.Add(new UserClaim { Type = type, Value = value });
        await _state.WriteStateAsync();
    }

    public async Task RemoveClaimAsync(string type)
    {
        if (_state.State.Claims.RemoveAll(c => c.Type == type) > 0)
        {
            await _state.WriteStateAsync();
        }
    }

    public Task<IReadOnlyList<UserClaim>> GetClaimsAsync()
    {
        return Task.FromResult<IReadOnlyList<UserClaim>>(_state.State.Claims);
    }
}

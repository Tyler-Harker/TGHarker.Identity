using System.Security.Cryptography;
using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Grains;

public sealed class OrganizationInvitationGrain : Grain, IOrganizationInvitationGrain
{
    private readonly IPersistentState<OrganizationInvitationState> _state;
    private readonly IGrainFactory _grainFactory;

    public OrganizationInvitationGrain(
        [PersistentState("org_invitation", "Default")] IPersistentState<OrganizationInvitationState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public Task<OrganizationInvitationState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.Id))
            return Task.FromResult<OrganizationInvitationState?>(null);

        return Task.FromResult<OrganizationInvitationState?>(_state.State);
    }

    public async Task<CreateOrganizationInvitationResult> CreateAsync(CreateOrganizationInvitationRequest request)
    {
        if (!string.IsNullOrEmpty(_state.State.Id))
        {
            return new CreateOrganizationInvitationResult
            {
                Success = false,
                Error = "Invitation already exists"
            };
        }

        var invitationId = Guid.CreateVersion7().ToString();
        var token = GenerateSecureToken();

        _state.State = new OrganizationInvitationState
        {
            Id = invitationId,
            TenantId = request.TenantId,
            OrganizationId = request.OrganizationId,
            Email = request.Email.ToLowerInvariant(),
            Token = token,
            InvitedByUserId = request.InvitedByUserId,
            Roles = request.Roles.ToList(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = InvitationStatus.Pending
        };

        await _state.WriteStateAsync();

        return new CreateOrganizationInvitationResult
        {
            Success = true,
            InvitationId = invitationId,
            Token = token
        };
    }

    public async Task<AcceptOrganizationInvitationResult> AcceptAsync(string userId)
    {
        if (string.IsNullOrEmpty(_state.State.Id))
        {
            return new AcceptOrganizationInvitationResult
            {
                Success = false,
                Error = "Invitation not found"
            };
        }

        if (_state.State.Status != InvitationStatus.Pending)
        {
            return new AcceptOrganizationInvitationResult
            {
                Success = false,
                Error = $"Invitation is no longer valid (status: {_state.State.Status})"
            };
        }

        if (DateTime.UtcNow > _state.State.ExpiresAt)
        {
            _state.State.Status = InvitationStatus.Expired;
            await _state.WriteStateAsync();

            return new AcceptOrganizationInvitationResult
            {
                Success = false,
                Error = "Invitation has expired"
            };
        }

        var tenantId = _state.State.TenantId;
        var organizationId = _state.State.OrganizationId;

        // Create organization membership
        var membershipKey = $"{tenantId}/org-{organizationId}/member-{userId}";
        var membershipGrain = _grainFactory.GetGrain<IOrganizationMembershipGrain>(membershipKey);
        var membershipResult = await membershipGrain.CreateAsync(new CreateOrganizationMembershipRequest
        {
            UserId = userId,
            OrganizationId = organizationId,
            TenantId = tenantId,
            Roles = _state.State.Roles
        });

        if (!membershipResult.Success)
        {
            return new AcceptOrganizationInvitationResult
            {
                Success = false,
                Error = membershipResult.Error ?? "Failed to create membership"
            };
        }

        // Add user to organization's member list
        var orgGrain = _grainFactory.GetGrain<IOrganizationGrain>($"{tenantId}/org-{organizationId}");
        await orgGrain.AddMemberAsync(userId);

        // Add organization ref to user's memberships
        // NOTE: This does NOT add the user to the tenant's member list - OrganizationUsers are separate from TenantUsers
        var userGrain = _grainFactory.GetGrain<IUserGrain>($"user-{userId}");
        await userGrain.AddOrganizationMembershipAsync(tenantId, organizationId);

        // Update invitation status
        _state.State.Status = InvitationStatus.Accepted;
        _state.State.AcceptedByUserId = userId;
        _state.State.AcceptedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return new AcceptOrganizationInvitationResult
        {
            Success = true,
            TenantId = tenantId,
            OrganizationId = organizationId
        };
    }

    public async Task<bool> RevokeAsync()
    {
        if (string.IsNullOrEmpty(_state.State.Id))
            return false;

        if (_state.State.Status != InvitationStatus.Pending)
            return false;

        _state.State.Status = InvitationStatus.Revoked;
        await _state.WriteStateAsync();

        return true;
    }

    public Task<bool> IsValidAsync()
    {
        if (string.IsNullOrEmpty(_state.State.Id))
            return Task.FromResult(false);

        if (_state.State.Status != InvitationStatus.Pending)
            return Task.FromResult(false);

        if (DateTime.UtcNow > _state.State.ExpiresAt)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_state.State.Id));
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}

using System.Security.Cryptography;
using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Grains;

public sealed class InvitationGrain : Grain, IInvitationGrain
{
    private readonly IPersistentState<InvitationState> _state;
    private readonly IGrainFactory _grainFactory;

    public InvitationGrain(
        [PersistentState("invitation", "Default")] IPersistentState<InvitationState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public Task<InvitationState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.Id))
            return Task.FromResult<InvitationState?>(null);

        return Task.FromResult<InvitationState?>(_state.State);
    }

    public async Task<CreateInvitationResult> CreateAsync(CreateInvitationRequest request)
    {
        if (!string.IsNullOrEmpty(_state.State.Id))
        {
            return new CreateInvitationResult
            {
                Success = false,
                Error = "Invitation already exists"
            };
        }

        var invitationId = Guid.CreateVersion7().ToString();
        var token = GenerateSecureToken();

        _state.State = new InvitationState
        {
            Id = invitationId,
            TenantId = request.TenantId,
            Email = request.Email.ToLowerInvariant(),
            Token = token,
            InvitedByUserId = request.InvitedByUserId,
            Roles = request.Roles.ToList(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = InvitationStatus.Pending
        };

        await _state.WriteStateAsync();

        return new CreateInvitationResult
        {
            Success = true,
            InvitationId = invitationId,
            Token = token
        };
    }

    public async Task<AcceptInvitationResult> AcceptAsync(string userId)
    {
        if (string.IsNullOrEmpty(_state.State.Id))
        {
            return new AcceptInvitationResult
            {
                Success = false,
                Error = "Invitation not found"
            };
        }

        if (_state.State.Status != InvitationStatus.Pending)
        {
            return new AcceptInvitationResult
            {
                Success = false,
                Error = $"Invitation is no longer valid (status: {_state.State.Status})"
            };
        }

        if (DateTime.UtcNow > _state.State.ExpiresAt)
        {
            _state.State.Status = InvitationStatus.Expired;
            await _state.WriteStateAsync();

            return new AcceptInvitationResult
            {
                Success = false,
                Error = "Invitation has expired"
            };
        }

        var tenantId = _state.State.TenantId;

        // Create membership
        var membershipGrain = _grainFactory.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
        var membershipResult = await membershipGrain.CreateAsync(new CreateMembershipRequest
        {
            UserId = userId,
            TenantId = tenantId,
            Roles = _state.State.Roles
        });

        if (!membershipResult.Success)
        {
            return new AcceptInvitationResult
            {
                Success = false,
                Error = membershipResult.Error ?? "Failed to create membership"
            };
        }

        // Add tenant to user's memberships
        var userGrain = _grainFactory.GetGrain<IUserGrain>($"user-{userId}");
        await userGrain.AddTenantMembershipAsync(tenantId);

        // Add user to tenant's member list
        var tenantGrain = _grainFactory.GetGrain<ITenantGrain>(tenantId);
        await tenantGrain.AddMemberAsync(userId);

        // Update invitation status
        _state.State.Status = InvitationStatus.Accepted;
        _state.State.AcceptedByUserId = userId;
        _state.State.AcceptedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return new AcceptInvitationResult
        {
            Success = true,
            TenantId = tenantId
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

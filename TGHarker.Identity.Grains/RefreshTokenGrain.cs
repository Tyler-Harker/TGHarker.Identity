using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Grains;

public sealed class RefreshTokenGrain : Grain, IRefreshTokenGrain
{
    private readonly IPersistentState<RefreshTokenState> _state;

    public RefreshTokenGrain(
        [PersistentState("refreshToken", "Default")] IPersistentState<RefreshTokenState> state)
    {
        _state = state;
    }

    public async Task<bool> CreateAsync(RefreshTokenState state)
    {
        if (!string.IsNullOrEmpty(_state.State.ClientId))
            return false;

        _state.State = state;
        await _state.WriteStateAsync();

        return true;
    }

    public async Task<RefreshTokenState?> ValidateAndRevokeAsync()
    {
        // Check if already revoked - potential token reuse attack
        if (_state.State.IsRevoked)
        {
            // If this token was replaced, someone is trying to reuse a rotated token
            // This could indicate token theft - the replacement token chain should also be revoked
            if (!string.IsNullOrEmpty(_state.State.ReplacedByTokenHash))
            {
                // Revoke the replacement token chain to protect against stolen tokens
                await RevokeTokenChainAsync(_state.State.ReplacedByTokenHash);
            }
            return null;
        }

        // Check expiration
        if (_state.State.ExpiresAt < DateTime.UtcNow)
            return null;

        // Revoke token (single use with rotation)
        _state.State.IsRevoked = true;
        _state.State.RevokedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return _state.State;
    }

    public async Task SetReplacementTokenAsync(string replacementTokenHash)
    {
        _state.State.ReplacedByTokenHash = replacementTokenHash;
        await _state.WriteStateAsync();
    }

    private async Task RevokeTokenChainAsync(string tokenHash)
    {
        // Revoke the replacement token and any tokens in its chain
        var nextToken = GrainFactory.GetGrain<IRefreshTokenGrain>($"{_state.State.TenantId}/rt-{tokenHash}");
        var nextState = await nextToken.GetStateAsync();

        if (nextState != null && !nextState.IsRevoked)
        {
            await nextToken.RevokeAsync();

            // Continue revoking the chain if there are more replacement tokens
            if (!string.IsNullOrEmpty(nextState.ReplacedByTokenHash))
            {
                await RevokeTokenChainAsync(nextState.ReplacedByTokenHash);
            }
        }
    }

    public async Task RevokeAsync()
    {
        if (!_state.State.IsRevoked)
        {
            _state.State.IsRevoked = true;
            _state.State.RevokedAt = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> IsValidAsync()
    {
        if (_state.State.IsRevoked)
            return Task.FromResult(false);

        if (_state.State.ExpiresAt < DateTime.UtcNow)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public Task<RefreshTokenState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.ClientId))
            return Task.FromResult<RefreshTokenState?>(null);

        return Task.FromResult<RefreshTokenState?>(_state.State);
    }
}

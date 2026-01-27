using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TGHarker.Identity.Grains;

public sealed class AuthorizationCodeGrain : Grain, IAuthorizationCodeGrain
{
    private readonly IPersistentState<AuthorizationCodeState> _state;

    public AuthorizationCodeGrain(
        [PersistentState("authCode", "Default")] IPersistentState<AuthorizationCodeState> state)
    {
        _state = state;
    }

    public async Task<bool> CreateAsync(AuthorizationCodeState state)
    {
        if (!string.IsNullOrEmpty(_state.State.ClientId))
            return false;

        _state.State = state;
        await _state.WriteStateAsync();

        // Schedule deactivation after expiry
        var delay = state.ExpiresAt - DateTime.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            this.RegisterGrainTimer(
                static (state, _) => state.DeactivateOnExpiry(),
                this,
                new GrainTimerCreationOptions
                {
                    DueTime = delay,
                    Period = Timeout.InfiniteTimeSpan,
                    Interleave = true
                });
        }

        return true;
    }

    public async Task<AuthorizationCodeState?> RedeemAsync(string? codeVerifier)
    {
        // Check if already redeemed
        if (_state.State.IsRedeemed)
            return null;

        // Check expiration
        if (_state.State.ExpiresAt < DateTime.UtcNow)
            return null;

        // Validate PKCE if code challenge was provided
        if (!string.IsNullOrEmpty(_state.State.CodeChallenge))
        {
            if (string.IsNullOrEmpty(codeVerifier))
                return null;

            if (!ValidatePkce(codeVerifier, _state.State.CodeChallenge, _state.State.CodeChallengeMethod))
                return null;
        }

        // Mark as redeemed
        _state.State.IsRedeemed = true;
        _state.State.RedeemedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return _state.State;
    }

    public Task<bool> IsValidAsync()
    {
        if (_state.State.IsRedeemed)
            return Task.FromResult(false);

        if (_state.State.ExpiresAt < DateTime.UtcNow)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public Task<AuthorizationCodeState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.ClientId))
            return Task.FromResult<AuthorizationCodeState?>(null);

        return Task.FromResult<AuthorizationCodeState?>(_state.State);
    }

    private Task DeactivateOnExpiry()
    {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }

    private static bool ValidatePkce(string codeVerifier, string codeChallenge, string? codeChallengeMethod)
    {
        // Validate code_verifier format: 43-128 characters
        if (codeVerifier.Length < 43 || codeVerifier.Length > 128)
            return false;

        string computedChallenge;

        if (codeChallengeMethod == "S256" || string.IsNullOrEmpty(codeChallengeMethod))
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            computedChallenge = Base64UrlEncoder.Encode(hash);
        }
        else if (codeChallengeMethod == "plain")
        {
            computedChallenge = codeVerifier;
        }
        else
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedChallenge),
            Encoding.UTF8.GetBytes(codeChallenge));
    }
}

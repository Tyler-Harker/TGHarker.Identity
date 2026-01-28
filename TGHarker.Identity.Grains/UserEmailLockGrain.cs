using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Grains;

public sealed class UserEmailLockGrain : Grain, IUserEmailLockGrain
{
    private readonly IPersistentState<LockState<string>> _state;

    public UserEmailLockGrain(
        [PersistentState("emailLock", "Default")] IPersistentState<LockState<string>> state)
    {
        _state = state;
    }

    public async Task<LockResult> TryLockAsync(string owner)
    {
        if (string.IsNullOrEmpty(owner))
            return new LockResult { Success = false, Error = "Owner cannot be empty" };

        // Already locked by same owner - idempotent success
        if (_state.State.Status == LockStatus.Locked && _state.State.Owner == owner)
            return LockResult.AlreadyOwned();

        // Locked by different owner
        if (_state.State.Status == LockStatus.Locked)
            return LockResult.LockedByOther(_state.State.Owner!);

        // Acquire lock
        _state.State.Status = LockStatus.Locked;
        _state.State.Owner = owner;
        _state.State.LockedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return LockResult.Acquired();
    }

    public async Task<bool> UnlockAsync(string owner)
    {
        if (_state.State.Status != LockStatus.Locked)
            return true; // Already unlocked

        if (_state.State.Owner != owner)
            return false; // Not the owner

        _state.State.Status = LockStatus.Unlocked;
        _state.State.Owner = default;
        _state.State.LockedAt = null;
        await _state.WriteStateAsync();

        return true;
    }

    public Task<LockState<string>> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public Task<bool> IsLockedAsync()
    {
        return Task.FromResult(_state.State.Status == LockStatus.Locked);
    }

    public Task<string?> GetOwnerAsync()
    {
        return Task.FromResult(_state.State.Owner);
    }
}

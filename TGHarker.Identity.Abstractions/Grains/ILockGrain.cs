using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Generic lock grain interface for distributed locking of resources.
/// The grain key represents the resource being locked.
/// </summary>
/// <typeparam name="TOwner">The type of the owner identifier</typeparam>
public interface ILockGrain<TOwner> : IGrainWithStringKey
{
    /// <summary>
    /// Attempts to acquire the lock for the specified owner.
    /// If already locked by the same owner, returns success.
    /// If locked by a different owner, returns failure with current owner info.
    /// </summary>
    Task<LockResult> TryLockAsync(TOwner owner);

    /// <summary>
    /// Releases the lock if owned by the specified owner.
    /// </summary>
    Task<bool> UnlockAsync(TOwner owner);

    /// <summary>
    /// Gets the current lock state.
    /// </summary>
    Task<LockState<TOwner>> GetStateAsync();

    /// <summary>
    /// Checks if the resource is currently locked.
    /// </summary>
    Task<bool> IsLockedAsync();

    /// <summary>
    /// Gets the current owner, or default if unlocked.
    /// </summary>
    Task<TOwner?> GetOwnerAsync();
}

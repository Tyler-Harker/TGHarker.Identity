namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Lock grain for user email addresses.
/// The grain key is the email address (lowercase).
/// Tracks which userId has locked the email during registration.
/// </summary>
public interface IUserEmailLockGrain : ILockGrain<string>
{
}

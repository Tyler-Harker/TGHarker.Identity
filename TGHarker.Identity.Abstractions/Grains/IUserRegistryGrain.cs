namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Singleton grain that maps email addresses to user IDs.
/// Key: "user-registry"
/// </summary>
public interface IUserRegistryGrain : IGrainWithStringKey
{
    Task<bool> RegisterUserAsync(string email, string userId);
    Task<string?> GetUserIdByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);
    Task RemoveUserAsync(string email);
}

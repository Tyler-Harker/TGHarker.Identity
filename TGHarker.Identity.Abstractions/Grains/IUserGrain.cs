using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Global user grain representing a user's core identity across all tenants.
/// Key: user-{userId} (e.g., "user-abc123")
/// </summary>
public interface IUserGrain : IGrainWithStringKey
{
    Task<UserState?> GetStateAsync();
    Task<CreateUserResult> CreateAsync(CreateUserRequest request);
    Task<bool> ValidatePasswordAsync(string password);
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword);
    Task SetPasswordHashAsync(string passwordHash);
    Task<string?> GenerateEmailVerificationTokenAsync();
    Task<bool> VerifyEmailAsync(string token);
    Task<string?> GeneratePasswordResetTokenAsync();
    Task<bool> ResetPasswordAsync(string token, string newPasswordHash);
    Task UpdateProfileAsync(UpdateProfileRequest request);
    Task RecordLoginAttemptAsync(bool success, string? ipAddress);
    Task<bool> IsLockedOutAsync();
    Task UnlockAsync();
    Task<bool> ExistsAsync();

    // Tenant membership management
    Task<IReadOnlyList<string>> GetTenantMembershipsAsync();
    Task AddTenantMembershipAsync(string tenantId);
    Task RemoveTenantMembershipAsync(string tenantId);

    // Organization membership management
    Task<IReadOnlyList<OrganizationMembershipRef>> GetOrganizationMembershipsAsync();
    Task<IReadOnlyList<OrganizationMembershipRef>> GetOrganizationMembershipsInTenantAsync(string tenantId);
    Task AddOrganizationMembershipAsync(string tenantId, string organizationId);
    Task RemoveOrganizationMembershipAsync(string tenantId, string organizationId);
}

using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing an OAuth client/application within a tenant.
/// Key: {tenantId}/{clientId} (e.g., "acme/my-spa-app")
/// </summary>
public interface IClientGrain : IGrainWithStringKey
{
    Task<ClientState?> GetStateAsync();
    Task<CreateClientResult> CreateAsync(CreateClientRequest request);
    Task<string?> UpdateAsync(UpdateClientRequest request);
    Task<bool> ValidateSecretAsync(string secret);
    Task<AddSecretResult> AddSecretAsync(string description, DateTime? expiresAt);
    Task<bool> RevokeSecretAsync(string secretId);
    Task<bool> ValidateRedirectUriAsync(string redirectUri);
    Task<bool> ValidateScopeAsync(string scope);
    Task<bool> ValidateGrantTypeAsync(string grantType);
    Task<bool> ValidatePostLogoutRedirectUriAsync(string uri);
    Task<bool> IsActiveAsync();
    Task ActivateAsync();
    Task DeactivateAsync();
    Task<bool> ExistsAsync();
    Task<UserFlowSettings> GetUserFlowSettingsAsync();

    // Application Permission Management
    /// <summary>
    /// Adds a new permission to the application.
    /// </summary>
    Task<ApplicationPermission> AddPermissionAsync(string name, string? displayName, string? description);

    /// <summary>
    /// Removes a permission from the application.
    /// </summary>
    Task<bool> RemovePermissionAsync(string name);

    /// <summary>
    /// Gets all permissions defined for this application.
    /// </summary>
    Task<IReadOnlyList<ApplicationPermission>> GetApplicationPermissionsAsync();

    // Application Role Management
    /// <summary>
    /// Creates a new application role.
    /// </summary>
    Task<ApplicationRole> CreateApplicationRoleAsync(string name, string? displayName, string? description, List<string> permissions);

    /// <summary>
    /// Updates an existing application role.
    /// </summary>
    Task<ApplicationRole?> UpdateApplicationRoleAsync(string roleId, string? name, string? displayName, string? description, List<string>? permissions);

    /// <summary>
    /// Deletes an application role.
    /// </summary>
    Task<bool> DeleteApplicationRoleAsync(string roleId);

    /// <summary>
    /// Gets all roles defined for this application.
    /// </summary>
    Task<IReadOnlyList<ApplicationRole>> GetApplicationRolesAsync();

    /// <summary>
    /// Gets a specific application role by ID.
    /// </summary>
    Task<ApplicationRole?> GetApplicationRoleAsync(string roleId);

    /// <summary>
    /// Sets the default application role to assign to new users.
    /// </summary>
    Task SetDefaultApplicationRoleAsync(string? roleId);

    /// <summary>
    /// Sets whether permissions should be included in access tokens.
    /// </summary>
    Task SetIncludePermissionsInTokenAsync(bool include);

    // System Permissions/Roles Management (for application self-registration via CCF)
    /// <summary>
    /// Synchronizes system permissions from the application. Adds new ones, updates existing, removes stale.
    /// Only system permissions are affected; user-created permissions are left untouched.
    /// </summary>
    Task<SyncPermissionsResult> SyncSystemPermissionsAsync(IReadOnlyList<ApplicationPermission> permissions);

    /// <summary>
    /// Synchronizes system roles from the application. Adds new ones, updates existing, removes stale.
    /// Only system roles are affected; user-created roles are left untouched.
    /// </summary>
    Task<SyncRolesResult> SyncSystemRolesAsync(IReadOnlyList<ApplicationRole> roles);
}

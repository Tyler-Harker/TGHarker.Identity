namespace TGHarker.Identity.Client;

/// <summary>
/// Client for interacting with the TGHarker.Identity server.
/// </summary>
public interface IIdentityClient
{
    /// <summary>
    /// Synchronizes the application's system permissions with the identity server.
    /// Adds new permissions, updates existing ones, and removes stale ones.
    /// </summary>
    /// <param name="permissions">The permissions to sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sync result indicating what changed.</returns>
    Task<SyncResult> SyncPermissionsAsync(IEnumerable<Permission> permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes the application's system roles with the identity server.
    /// Adds new roles, updates existing ones, and removes stale ones.
    /// </summary>
    /// <param name="roles">The roles to sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sync result indicating what changed.</returns>
    Task<SyncResult> SyncRolesAsync(IEnumerable<Role> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes both permissions and roles in one call.
    /// </summary>
    /// <param name="permissions">The permissions to sync.</param>
    /// <param name="roles">The roles to sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the permissions and roles sync results.</returns>
    Task<(SyncResult Permissions, SyncResult Roles)> SyncAllAsync(
        IEnumerable<Permission> permissions,
        IEnumerable<Role> roles,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a sync operation.
/// </summary>
public sealed class SyncResult
{
    /// <summary>
    /// Whether the sync operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of items added.
    /// </summary>
    public int Added { get; set; }

    /// <summary>
    /// Number of items updated.
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// Number of items removed.
    /// </summary>
    public int Removed { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Creates a successful sync result.
    /// </summary>
    public static SyncResult Successful(int added = 0, int updated = 0, int removed = 0)
        => new() { Success = true, Added = added, Updated = updated, Removed = removed };

    /// <summary>
    /// Creates a failed sync result.
    /// </summary>
    public static SyncResult Failed(string error)
        => new() { Success = false, Error = error };
}

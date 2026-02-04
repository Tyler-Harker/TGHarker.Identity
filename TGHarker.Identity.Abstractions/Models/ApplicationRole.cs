namespace TGHarker.Identity.Abstractions.Models;

/// <summary>
/// Represents a role defined by an application/client, containing a set of permissions.
/// </summary>
[GenerateSerializer]
public sealed class ApplicationRole
{
    /// <summary>
    /// Unique identifier for this role.
    /// </summary>
    [Id(0)] public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the role (e.g., "admin", "viewer").
    /// </summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the role.
    /// </summary>
    [Id(2)] public string? DisplayName { get; set; }

    /// <summary>
    /// Description of what this role represents.
    /// </summary>
    [Id(3)] public string? Description { get; set; }

    /// <summary>
    /// Indicates if this is a system-defined role that cannot be deleted.
    /// </summary>
    [Id(4)] public bool IsSystem { get; set; }

    /// <summary>
    /// List of permission names assigned to this role.
    /// </summary>
    [Id(5)] public List<string> Permissions { get; set; } = [];

    /// <summary>
    /// When this role was created.
    /// </summary>
    [Id(6)] public DateTime CreatedAt { get; set; }
}

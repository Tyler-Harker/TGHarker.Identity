namespace TGHarker.Identity.Abstractions.Models;

/// <summary>
/// Represents a permission defined by an application/client.
/// </summary>
[GenerateSerializer]
public sealed class ApplicationPermission
{
    /// <summary>
    /// The unique name of the permission (e.g., "project:read", "admin:users").
    /// </summary>
    [Id(0)] public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the permission.
    /// </summary>
    [Id(1)] public string? DisplayName { get; set; }

    /// <summary>
    /// Description of what this permission allows.
    /// </summary>
    [Id(2)] public string? Description { get; set; }
}

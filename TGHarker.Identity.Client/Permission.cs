namespace TGHarker.Identity.Client;

/// <summary>
/// Represents a permission that can be registered with the identity server.
/// </summary>
public sealed class Permission
{
    /// <summary>
    /// The unique name of the permission (e.g., "project:read", "admin:users").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the permission.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description of what this permission allows.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Creates a new permission with the specified name.
    /// </summary>
    public Permission(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a new permission with the specified name, display name, and description.
    /// </summary>
    public Permission(string name, string? displayName, string? description)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// Creates a new permission with default values.
    /// </summary>
    public Permission() { }
}

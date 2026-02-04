namespace TGHarker.Identity.Client;

/// <summary>
/// Represents a role that can be registered with the identity server.
/// </summary>
public sealed class Role
{
    /// <summary>
    /// Optional unique identifier for the role. If not provided, one will be generated.
    /// Use a consistent ID if you want to update the same role across deployments.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// The unique name of the role (e.g., "admin", "viewer", "editor").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the role.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description of what this role represents.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The list of permission names that this role grants.
    /// </summary>
    public List<string> Permissions { get; set; } = [];

    /// <summary>
    /// Creates a new role with the specified name.
    /// </summary>
    public Role(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a new role with the specified name and permissions.
    /// </summary>
    public Role(string name, params string[] permissions)
    {
        Name = name;
        Permissions = [.. permissions];
    }

    /// <summary>
    /// Creates a new role with default values.
    /// </summary>
    public Role() { }

    /// <summary>
    /// Adds a permission to this role.
    /// </summary>
    public Role WithPermission(string permission)
    {
        Permissions.Add(permission);
        return this;
    }

    /// <summary>
    /// Adds multiple permissions to this role.
    /// </summary>
    public Role WithPermissions(params string[] permissions)
    {
        Permissions.AddRange(permissions);
        return this;
    }
}

namespace TGHarker.Identity.Client;

/// <summary>
/// Builder for configuring the identity client's permissions and roles.
/// </summary>
public sealed class IdentityClientBuilder
{
    private readonly List<Permission> _permissions = [];
    private readonly List<Role> _roles = [];

    /// <summary>
    /// Gets the configured permissions.
    /// </summary>
    public IReadOnlyList<Permission> Permissions => _permissions;

    /// <summary>
    /// Gets the configured roles.
    /// </summary>
    public IReadOnlyList<Role> Roles => _roles;

    /// <summary>
    /// Adds a permission to the configuration.
    /// </summary>
    /// <param name="name">The permission name.</param>
    /// <param name="displayName">Optional display name.</param>
    /// <param name="description">Optional description.</param>
    public IdentityClientBuilder AddPermission(string name, string? displayName = null, string? description = null)
    {
        _permissions.Add(new Permission(name, displayName, description));
        return this;
    }

    /// <summary>
    /// Adds a permission to the configuration.
    /// </summary>
    public IdentityClientBuilder AddPermission(Permission permission)
    {
        _permissions.Add(permission);
        return this;
    }

    /// <summary>
    /// Adds multiple permissions to the configuration.
    /// </summary>
    public IdentityClientBuilder AddPermissions(params Permission[] permissions)
    {
        _permissions.AddRange(permissions);
        return this;
    }

    /// <summary>
    /// Adds multiple permissions to the configuration.
    /// </summary>
    public IdentityClientBuilder AddPermissions(IEnumerable<Permission> permissions)
    {
        _permissions.AddRange(permissions);
        return this;
    }

    /// <summary>
    /// Adds a role to the configuration.
    /// </summary>
    /// <param name="name">The role name.</param>
    /// <param name="permissions">Permissions to include in the role.</param>
    public IdentityClientBuilder AddRole(string name, params string[] permissions)
    {
        _roles.Add(new Role(name, permissions));
        return this;
    }

    /// <summary>
    /// Adds a role to the configuration.
    /// </summary>
    public IdentityClientBuilder AddRole(Role role)
    {
        _roles.Add(role);
        return this;
    }

    /// <summary>
    /// Adds a role using a builder action.
    /// </summary>
    public IdentityClientBuilder AddRole(string name, Action<Role> configure)
    {
        var role = new Role(name);
        configure(role);
        _roles.Add(role);
        return this;
    }

    /// <summary>
    /// Adds multiple roles to the configuration.
    /// </summary>
    public IdentityClientBuilder AddRoles(params Role[] roles)
    {
        _roles.AddRange(roles);
        return this;
    }

    /// <summary>
    /// Adds multiple roles to the configuration.
    /// </summary>
    public IdentityClientBuilder AddRoles(IEnumerable<Role> roles)
    {
        _roles.AddRange(roles);
        return this;
    }
}

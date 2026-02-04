# Application Roles & Permissions

## Overview

TGHarker.Identity supports application-specific permissions and roles that allow client applications to implement fine-grained authorization. Unlike tenant-level roles which control access to the identity platform itself, application roles control what users can do within your application.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         Tenant                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                    Client/Application                │    │
│  │  ┌─────────────────┐  ┌─────────────────────────┐   │    │
│  │  │   Permissions   │  │         Roles           │   │    │
│  │  │ ─────────────── │  │ ─────────────────────── │   │    │
│  │  │ project:read    │  │ viewer                  │   │    │
│  │  │ project:write   │  │   └─ project:read       │   │    │
│  │  │ admin:users     │  │ editor                  │   │    │
│  │  └─────────────────┘  │   ├─ project:read       │   │    │
│  │                       │   └─ project:write      │   │    │
│  │                       │ admin                   │   │    │
│  │                       │   ├─ project:read       │   │    │
│  │                       │   ├─ project:write      │   │    │
│  │                       │   └─ admin:users        │   │    │
│  │                       └─────────────────────────┘   │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              User Application Roles                  │    │
│  │  User: alice@example.com                            │    │
│  │  ┌────────────────────────────────────────────────┐ │    │
│  │  │ Role: editor (Scope: Tenant)                   │ │    │
│  │  │ Role: admin  (Scope: Org: engineering)         │ │    │
│  │  └────────────────────────────────────────────────┘ │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## Data Models

### ApplicationPermission

Represents a single permission that can be performed within an application.

| Property | Type | Description |
|----------|------|-------------|
| `Name` | string | Unique identifier (e.g., `project:read`) |
| `DisplayName` | string? | Human-readable name |
| `Description` | string? | What this permission allows |
| `IsSystem` | bool | Whether this is managed by the application itself |

### ApplicationRole

A named grouping of permissions.

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique identifier |
| `Name` | string | Role name (e.g., `editor`) |
| `DisplayName` | string? | Human-readable name |
| `Description` | string? | Role description |
| `Permissions` | List<string> | Permission names included in this role |
| `IsSystem` | bool | Whether this is managed by the application itself |
| `CreatedAt` | DateTime | When the role was created |

### ApplicationRoleAssignment

Links a user to an application role within a specific scope.

| Property | Type | Description |
|----------|------|-------------|
| `RoleId` | string | The role being assigned |
| `Scope` | ApplicationRoleScope | `Tenant` or `Organization` |
| `OrganizationId` | string? | Required when scope is `Organization` |
| `AssignedAt` | DateTime | When the assignment was made |

## Permission Naming Conventions

We recommend using a `resource:action` naming pattern:

```
project:read      - Read project data
project:write     - Create/update projects
project:delete    - Delete projects
user:read         - View user profiles
user:admin        - Manage users
billing:view      - View billing information
billing:manage    - Modify billing settings
```

## System vs User-Created Items

### System Permissions/Roles

- Created and managed by the application itself via Client Credentials Flow
- Cannot be deleted or renamed by tenant administrators
- Automatically synced when the application starts
- Use these for core application functionality

### User-Created Permissions/Roles

- Created by tenant administrators via the dashboard
- Can be modified or deleted at any time
- Use these for tenant-specific customizations

## API Endpoints

### Permission Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/clients/{clientId}/permissions` | List all permissions |
| POST | `/api/clients/{clientId}/permissions` | Add a permission |
| DELETE | `/api/clients/{clientId}/permissions/{name}` | Remove a permission |

### Role Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/clients/{clientId}/roles` | List all roles |
| POST | `/api/clients/{clientId}/roles` | Create a role |
| PUT | `/api/clients/{clientId}/roles/{roleId}` | Update a role |
| DELETE | `/api/clients/{clientId}/roles/{roleId}` | Delete a role |

### User Role Assignment

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/clients/{clientId}/users/{userId}/roles` | Get user's role assignments |
| POST | `/api/clients/{clientId}/users/{userId}/roles` | Assign a role to user |
| DELETE | `/api/clients/{clientId}/users/{userId}/roles/{roleId}` | Remove role from user |

### System Sync (Client Credentials Flow)

| Method | Endpoint | Description |
|--------|----------|-------------|
| PUT | `/api/clients/{clientId}/system/permissions` | Sync system permissions |
| PUT | `/api/clients/{clientId}/system/roles` | Sync system roles |

## Token Claims

When `IncludePermissionsInToken` is enabled (default), access tokens include:

```json
{
  "sub": "user-123",
  "client_id": "my-app",
  "tenant_id": "acme",
  "organization_id": "engineering",
  "permissions": [
    "project:read",
    "project:write",
    "admin:users"
  ],
  "app_roles": [
    "editor",
    "admin"
  ]
}
```

### Effective Permissions Calculation

When a user authenticates with an organization context, their effective permissions are calculated as:

1. All permissions from **tenant-scoped** role assignments
2. Plus all permissions from **organization-scoped** role assignments for the current organization

```
Effective Permissions =
    Permissions(Tenant-scoped roles)
    ∪ Permissions(Organization-scoped roles for current org)
```

## Client Library

### Installation

```bash
dotnet add package TGHarker.Identity.Client
```

### Basic Usage

```csharp
// In Program.cs or Startup.cs
services.AddTGHarkerIdentityClient(
    options => {
        options.Authority = "https://identity.example.com";
        options.ClientId = "my-app";
        options.ClientSecret = Configuration["Identity:ClientSecret"];
        options.TenantId = "my-tenant";
        options.SyncOnStartup = true; // Default: true
    },
    builder => builder
        .AddPermission("project:read", "Read Projects", "View project data")
        .AddPermission("project:write", "Write Projects", "Create and modify projects")
        .AddPermission("project:delete", "Delete Projects", "Remove projects")
        .AddRole(new Role("viewer") {
            DisplayName = "Viewer",
            Description = "Can view projects",
            Permissions = ["project:read"]
        })
        .AddRole(new Role("editor") {
            DisplayName = "Editor",
            Description = "Can view and edit projects",
            Permissions = ["project:read", "project:write"]
        })
        .AddRole(new Role("admin") {
            DisplayName = "Administrator",
            Description = "Full access to projects",
            Permissions = ["project:read", "project:write", "project:delete"]
        })
);
```

### Manual Sync

If you need to sync permissions/roles manually (e.g., after configuration changes):

```csharp
public class MyService
{
    private readonly IIdentityClient _identityClient;

    public MyService(IIdentityClient identityClient)
    {
        _identityClient = identityClient;
    }

    public async Task UpdatePermissionsAsync()
    {
        var permissions = new[]
        {
            new Permission("new:permission", "New Permission", "A newly added permission")
        };

        var result = await _identityClient.SyncPermissionsAsync(permissions);

        if (result.Success)
        {
            Console.WriteLine($"Added: {result.Added}, Updated: {result.Updated}, Removed: {result.Removed}");
        }
    }
}
```

## Dashboard Management

Tenant administrators can manage application permissions and roles via the dashboard:

1. Navigate to **Applications** > Select application > **Roles & Permissions**
2. View and create custom permissions
3. Create custom roles that combine permissions
4. Assign roles to users with tenant or organization scope

**Note:** System permissions and roles (those created by the application) are shown with a "System" badge and cannot be deleted or renamed through the dashboard.

## Best Practices

1. **Use consistent naming** - Follow the `resource:action` pattern
2. **Start with system roles** - Define core roles in your application code
3. **Allow tenant customization** - Let admins create additional roles for their needs
4. **Keep permissions granular** - Prefer many small permissions over few large ones
5. **Document permissions** - Use clear display names and descriptions
6. **Use organization scope** - When users need different access in different teams

## Security Considerations

1. **Token size** - Many permissions can increase token size. Consider pagination or separate permission endpoints for applications with hundreds of permissions.
2. **Permission caching** - Permissions are computed at token issuance. Changes to role assignments take effect on next token refresh.
3. **Client Credentials validation** - System sync endpoints validate that `sub == client_id` to ensure only the application can modify its own system permissions.

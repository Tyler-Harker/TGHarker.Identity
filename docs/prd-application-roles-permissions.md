# PRD: Application-Scoped Permissions and Roles

## Document Info

| Field | Value |
|-------|-------|
| Status | Implemented |
| Author | TGHarker |
| Created | 2026-02-03 |

## Problem Statement

Client applications integrating with TGHarker.Identity need fine-grained authorization capabilities specific to their domain. While tenant-level roles control access to the identity platform, applications need their own permission system to control what users can do within the application itself.

### Current Limitations

- Tenant roles are designed for identity platform access, not application-specific authorization
- Applications must implement their own permission storage and management
- No standard way to include application permissions in access tokens
- No mechanism for applications to self-register their permission requirements

## Goals

1. Enable applications to define their own permissions and roles
2. Include effective permissions in access tokens for client-side enforcement
3. Support both tenant-wide and organization-specific role assignments
4. Allow applications to self-register system permissions via Client Credentials Flow
5. Provide a client library for easy integration

## Non-Goals

- Replace tenant-level roles for identity platform access
- Provide runtime permission checking APIs (applications handle this)
- Support cross-tenant permission sharing
- Implement permission inheritance hierarchies

## Solution Overview

### Core Components

1. **Application Permissions** - Named actions users can perform
2. **Application Roles** - Groups of permissions with metadata
3. **Role Assignments** - Links users to roles with scope (tenant/organization)
4. **Token Enhancement** - Include permissions in access tokens
5. **Self-Registration** - CCF endpoints for system permission sync
6. **Client Library** - NuGet package for automatic registration

### Data Model

```
ClientState
├── ApplicationPermissions[]
│   ├── Name (string, unique)
│   ├── DisplayName (string?)
│   ├── Description (string?)
│   └── IsSystem (bool)
├── ApplicationRoles[]
│   ├── Id (string)
│   ├── Name (string)
│   ├── DisplayName (string?)
│   ├── Description (string?)
│   ├── Permissions[] (string)
│   ├── IsSystem (bool)
│   └── CreatedAt (DateTime)
├── IncludePermissionsInToken (bool)
└── DefaultApplicationRoleId (string?)

UserApplicationRolesState (per user per client)
├── UserId (string)
├── TenantId (string)
├── ClientId (string)
└── RoleAssignments[]
    ├── RoleId (string)
    ├── Scope (Tenant | Organization)
    ├── OrganizationId (string?)
    └── AssignedAt (DateTime)
```

## User Stories

### As an application developer...

1. I want to define permissions in my application code so they are version-controlled
2. I want permissions automatically registered when my application starts
3. I want my system permissions protected from modification by tenant admins
4. I want user permissions included in access tokens for authorization decisions

### As a tenant administrator...

1. I want to view all permissions and roles defined by applications
2. I want to create custom roles that combine application permissions
3. I want to assign roles to users with appropriate scope
4. I want to see which items are system-managed vs custom

### As an end user...

1. I want my permissions to be automatically included when I authenticate
2. I want different access levels in different organizations
3. I want applications to enforce my permissions consistently

## API Design

### Permission Management

```http
GET    /api/clients/{clientId}/permissions
POST   /api/clients/{clientId}/permissions
DELETE /api/clients/{clientId}/permissions/{name}
```

### Role Management

```http
GET    /api/clients/{clientId}/roles
POST   /api/clients/{clientId}/roles
PUT    /api/clients/{clientId}/roles/{roleId}
DELETE /api/clients/{clientId}/roles/{roleId}
```

### User Role Assignment

```http
GET    /api/clients/{clientId}/users/{userId}/roles
POST   /api/clients/{clientId}/users/{userId}/roles
DELETE /api/clients/{clientId}/users/{userId}/roles/{roleId}
```

### System Sync (CCF Only)

```http
PUT /api/clients/{clientId}/system/permissions
PUT /api/clients/{clientId}/system/roles
```

### Request/Response Examples

**Sync Permissions Request:**
```json
{
  "permissions": [
    {
      "name": "project:read",
      "display_name": "Read Projects",
      "description": "View project data"
    },
    {
      "name": "project:write",
      "display_name": "Write Projects",
      "description": "Create and modify projects"
    }
  ]
}
```

**Sync Response:**
```json
{
  "success": true,
  "added": 2,
  "updated": 0,
  "removed": 1
}
```

**Access Token Claims:**
```json
{
  "sub": "user-123",
  "client_id": "my-app",
  "tenant_id": "acme",
  "organization_id": "engineering",
  "permissions": ["project:read", "project:write"],
  "app_roles": ["editor"]
}
```

## Implementation

### Phase 1: Core Data Model (Complete)

- [x] Create ApplicationPermission model with IsSystem flag
- [x] Create ApplicationRole model with IsSystem flag
- [x] Create ApplicationRoleAssignment model with scope
- [x] Create UserApplicationRolesState model
- [x] Extend ClientState with permission/role collections

### Phase 2: Grain Implementation (Complete)

- [x] Add permission/role management methods to IClientGrain
- [x] Implement methods in ClientGrain with system item protection
- [x] Create IUserApplicationRolesGrain interface
- [x] Implement UserApplicationRolesGrain with effective permission calculation
- [x] Add SyncSystemPermissionsAsync and SyncSystemRolesAsync methods

### Phase 3: Token Enhancement (Complete)

- [x] Modify TokenEndpoint to include permissions in access tokens
- [x] Add app_roles claim with role names
- [x] Support organization context for scoped permissions

### Phase 4: API Endpoints (Complete)

- [x] Create ApplicationRolesEndpoint with all CRUD operations
- [x] Add tenant-prefixed route variants
- [x] Implement system sync endpoints with CCF validation
- [x] Add proper authorization (admin required for management)

### Phase 5: Client Library (Complete)

- [x] Create TGHarker.Identity.Client NuGet package
- [x] Implement IdentityClientOptions for configuration
- [x] Implement Permission and Role models
- [x] Implement IIdentityClient with sync methods
- [x] Implement IdentityClientBuilder for fluent configuration
- [x] Implement IdentitySyncHostedService for auto-sync on startup
- [x] Create ServiceCollectionExtensions for DI registration

### Phase 6: Dashboard UI (Complete)

- [x] Create Permissions management page with pagination
- [x] Create Roles management pages (list, create, edit)
- [x] Create User Roles assignment pages
- [x] Show "System" badge for system items
- [x] Disable delete/edit for system items

## Success Metrics

1. **Adoption** - Number of applications using the client library
2. **Permission Coverage** - Average permissions per application
3. **API Performance** - Sync operation latency < 500ms
4. **Token Size** - Monitor access token size increase

## Security Considerations

1. **CCF Validation** - System sync requires `sub == client_id` match
2. **Admin Authorization** - Management APIs require tenant admin role
3. **System Protection** - System items cannot be modified via dashboard
4. **Scope Isolation** - Organization-scoped roles only apply to that org

## Future Enhancements

1. **Permission Groups** - Hierarchical permission organization
2. **Role Templates** - Pre-defined role templates for common patterns
3. **Audit Logging** - Track permission/role changes
4. **Permission Analytics** - Track permission usage patterns
5. **Bulk Operations** - Assign roles to multiple users at once

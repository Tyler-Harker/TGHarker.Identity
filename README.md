# TGHarker.Identity

A multi-tenant OAuth2/OpenID Connect identity provider built with .NET 10, Microsoft Orleans, and .NET Aspire.

## Overview

TGHarker.Identity is a self-hosted identity platform that provides complete authentication and authorization services with support for tenants, organizations, users, roles, and OAuth2 clients. It uses Orleans for distributed state management and is deployable to Azure Container Apps.

## Features

- **Multi-Tenancy** - Full tenant isolation with per-tenant configuration, clients, and roles
- **OAuth2/OIDC Compliance** - Authorization Code, Client Credentials, and Refresh Token grant types
- **PKCE Support** - Proof Key for Code Exchange for public clients
- **User Management** - Registration, email verification, password management, and account lockout
- **Organization Support** - Create organizations within tenants with membership and roles
- **Role-Based Access Control** - Tenant and organization-level roles with permissions
- **Application Roles & Permissions** - Define application-specific permissions and roles with automatic token inclusion
- **JWT Tokens** - RSA256-signed access tokens and ID tokens
- **Distributed Architecture** - Scalable via Orleans virtual actors

## Architecture

```
TGHarker.Identity.AppHost (Aspire Orchestrator)
├── TGHarker.Identity.Web      (ASP.NET Core + OAuth2/OIDC endpoints)
├── TGHarker.Identity.Silo     (Orleans Silo host)
└── Infrastructure
    ├── Azure Storage          (Blob state + Table clustering)
    └── PostgreSQL             (Search index)
```

## Project Structure

| Project | Description |
|---------|-------------|
| `TGHarker.Identity.AppHost` | Aspire application host and orchestration |
| `TGHarker.Identity.Web` | ASP.NET Core web app with OAuth2/OIDC endpoints and UI |
| `TGHarker.Identity.Silo` | Orleans silo server for grain execution |
| `TGHarker.Identity.Grains` | Domain logic implemented as Orleans grains |
| `TGHarker.Identity.Abstractions` | Shared interfaces, models, and DTOs |
| `TGHarker.Identity.Client` | Client library for registering application permissions/roles |
| `TGHarker.Identity.ServiceDefaults` | Aspire defaults and OpenTelemetry configuration |
| `TGHarker.Identity.Tests` | Unit tests |

## Technology Stack

- **.NET 10.0** - Target framework
- **ASP.NET Core 10** - Web framework
- **Microsoft Orleans 10.x** - Distributed virtual actor runtime
- **.NET Aspire 13.1** - Cloud-native orchestration
- **Azure Storage** - Grain state persistence and clustering
- **PostgreSQL** - Search index for queryable data
- **OpenTelemetry** - Distributed tracing and monitoring

## Prerequisites

- .NET 10.0 SDK
- Docker (for local development)
- Azure Storage Account (or emulator)
- PostgreSQL database

## Getting Started

### Local Development

Run the entire application stack using Aspire:

```bash
dotnet run --project TGHarker.Identity.AppHost
```

This automatically starts:
- PostgreSQL container
- Azure Storage emulator
- Orleans silo
- Web application
- Aspire dashboard at `http://localhost:15000`

### Azure Deployment

Deploy to Azure Container Apps using the Azure Developer CLI:

```bash
azd up
```

## OAuth2/OIDC Endpoints

| Endpoint | Path |
|----------|------|
| OpenID Configuration | `/.well-known/openid-configuration` |
| JWKS | `/.well-known/jwks.json` |
| Authorization | `/connect/authorize` |
| Token | `/connect/token` |
| UserInfo | `/connect/userinfo` |
| Revocation | `/connect/revocation` |
| Introspection | `/connect/introspect` |

### Supported Scopes

- `openid` - Required for OIDC
- `profile` - User profile claims
- `email` - Email address
- `phone` - Phone number
- `offline_access` - Refresh token issuance

### Authorization Code Flow with PKCE

1. Redirect user to `/connect/authorize` with PKCE challenge
2. User authenticates and grants consent
3. Receive authorization code via redirect
4. Exchange code + PKCE verifier for tokens at `/connect/token`
5. Receive `access_token`, `id_token`, and optionally `refresh_token`

## Organizations

Users can belong to multiple organizations within a tenant. The ID token includes organization-related claims to help your application understand the user's organizational context.

### Organization Claims in ID Token

| Claim | Description | Example |
|-------|-------------|---------|
| `organizations` | JSON array of all organizations the user belongs to | `[{"id":"acme","name":"Acme Corp"},{"id":"dev-team","name":"Dev Team"}]` |
| `organization` | JSON object of the currently selected organization | `{"id":"acme","name":"Acme Corp"}` |

### Organization Selection Flow

When a user belongs to multiple organizations, they can select which organization to use for the session:

1. **Automatic Selection** - If the user has only one organization, it's automatically selected
2. **Default Organization** - If the user has set a default organization, it's used automatically
3. **Organization Picker** - If the user has multiple organizations and no default, they're redirected to `/tenant/{tenantId}/select-organization` to choose one

### Letting Users Switch Organizations

To allow a user to switch to a different organization mid-session, redirect them to the authorization endpoint with `prompt=login` or implement one of these approaches:

#### Option 1: Re-authorize with Organization Picker

Force the organization picker to appear by not passing an `organization_id`:

```
/tenant/{tenantId}/connect/authorize?
  response_type=code
  &client_id={clientId}
  &redirect_uri={redirectUri}
  &scope=openid profile email
  &prompt=select_account
  ...
```

The user will be shown the organization picker if they belong to multiple organizations.

#### Option 2: Pre-select Organization

If you know which organization the user wants to switch to, pass it directly:

```
/tenant/{tenantId}/connect/authorize?
  response_type=code
  &client_id={clientId}
  &redirect_uri={redirectUri}
  &scope=openid profile email
  &organization_id={organizationId}
  ...
```

This bypasses the picker and directly selects the specified organization (user must be a member).

#### Option 3: Build a Custom Organization Switcher

Use the `organizations` claim from the current ID token to display a list of available organizations in your application. When the user selects one, re-authorize with the `organization_id` parameter set to the chosen organization.

```javascript
// Example: Parse organizations from ID token
const idToken = parseJwt(id_token);
const organizations = JSON.parse(idToken.organizations || '[]');

// Display org switcher UI
organizations.forEach(org => {
  console.log(`${org.name} (${org.id})`);
});

// When user selects an org, redirect to authorize with organization_id
window.location.href = `/tenant/${tenantId}/connect/authorize?...&organization_id=${selectedOrgId}`;
```

### Setting a Default Organization

Users can set a default organization when selecting one during login by checking "Remember this choice for future logins". This default is stored per-tenant and will be used automatically for future authorization requests.

## Application Roles & Permissions

Applications can define their own permissions and roles that are included in access tokens. This enables fine-grained authorization within your applications.

### Key Concepts

- **Application Permissions** - Actions users can perform (e.g., `project:read`, `admin:users`)
- **Application Roles** - Named groupings of permissions (e.g., `viewer`, `editor`, `admin`)
- **System Permissions/Roles** - Defined by the application itself via Client Credentials Flow, cannot be modified by tenant admins
- **User-Created Permissions/Roles** - Created by tenant admins via the dashboard

### Role Assignment Scopes

Roles can be assigned to users at two scopes:

| Scope | Description |
|-------|-------------|
| **Tenant** | Role applies across all organizations within the tenant |
| **Organization** | Role applies only within a specific organization |

### Token Claims

When a user authenticates, their effective permissions and roles are included in the access token:

```json
{
  "sub": "user-123",
  "tenant_id": "acme",
  "permissions": ["project:read", "project:write", "admin:users"],
  "app_roles": ["editor", "user-admin"]
}
```

### Managing via API

Applications can sync their system permissions and roles using Client Credentials Flow:

```
PUT /api/clients/{clientId}/system/permissions
PUT /api/clients/{clientId}/system/roles
```

These endpoints require a CCF token where `sub == client_id`, ensuring only the application itself can modify its system permissions/roles.

### Client Library (TGHarker.Identity.Client)

Use the client library to automatically register permissions and roles on application startup:

```csharp
services.AddTGHarkerIdentityClient(
    options => {
        options.Authority = "https://identity.example.com";
        options.ClientId = "my-app";
        options.ClientSecret = "secret";
        options.TenantId = "tenant-1";
    },
    builder => builder
        .AddPermission("project:read", "Read Projects", "View project data")
        .AddPermission("project:write", "Write Projects", "Modify project data")
        .AddPermission("admin:users", "Manage Users", "Create and manage users")
        .AddRole("viewer", "project:read")
        .AddRole("editor", "project:read", "project:write")
        .AddRole("admin", "project:read", "project:write", "admin:users")
);
```

The library automatically syncs permissions and roles with the identity server on startup.

## Configuration

### Default Token Lifetimes

| Token Type | Lifetime |
|------------|----------|
| Access Token | 60 minutes |
| ID Token | 60 minutes |
| Refresh Token | 30 days |
| Authorization Code | 5 minutes |

### Security Settings

- PKCE required by default
- Max 5 login attempts per minute (configurable)
- Secure password hashing

## Documentation

See the [docs](./docs/) folder for detailed documentation:

- [Application Roles & Permissions](./docs/application-roles-permissions.md) - Fine-grained authorization for applications
- [Application Roles PRD](./docs/prd-application-roles-permissions.md) - Product requirements document

## License

Proprietary

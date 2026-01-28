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

## License

Proprietary

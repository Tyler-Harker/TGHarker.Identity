# TGHarker.Identity.Client

Client library for TGHarker.Identity - enables applications to register their permissions and roles with the TGHarker Identity service.

## Installation

```bash
dotnet add package TGHarker.Identity.Client
```

## Usage

### Basic Configuration

```csharp
builder.Services.AddIdentityClient(options =>
{
    options.IdentityServerUrl = "https://your-identity-server.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
});
```

### Register Permissions and Roles

```csharp
builder.Services.AddIdentityClient(options =>
{
    options.IdentityServerUrl = "https://your-identity-server.com";
    options.ClientId = "your-client-id";
    options.ClientSecret = "your-client-secret";
})
.AddPermission("users.read", "Read users")
.AddPermission("users.write", "Write users")
.AddRole("Admin", "Administrator role", "users.read", "users.write")
.AddRole("Viewer", "Read-only role", "users.read");
```

## Features

- Automatic registration of permissions and roles
- HTTP client configuration for Identity service
- Background synchronization service
- Dependency injection integration

## License

MIT

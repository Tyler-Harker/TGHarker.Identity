using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
[Searchable(typeof(IClientGrain))]
public sealed class ClientState
{
    [Id(0)] public string Id { get; set; } = string.Empty;

    [Id(1)]
    [Queryable(Indexed = true)]
    public string TenantId { get; set; } = string.Empty;

    [Id(2)] public string ClientId { get; set; } = string.Empty;

    [Id(3)]
    [Queryable]
    public string? ClientName { get; set; }
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public bool IsConfidential { get; set; }

    [Id(6)]
    [Queryable]
    public bool IsActive { get; set; }
    [Id(7)] public bool RequireConsent { get; set; }
    [Id(8)] public bool RequirePkce { get; set; } = true;
    [Id(9)] public List<ClientSecret> Secrets { get; set; } = [];
    [Id(10)] public List<string> RedirectUris { get; set; } = [];
    [Id(11)] public List<string> AllowedScopes { get; set; } = [];
    [Id(12)] public List<string> AllowedGrantTypes { get; set; } = [];
    [Id(13)] public List<string> CorsOrigins { get; set; } = [];
    [Id(14)] public int? AccessTokenLifetimeMinutes { get; set; }
    [Id(15)] public int? RefreshTokenLifetimeDays { get; set; }
    [Id(16)] public int? IdTokenLifetimeMinutes { get; set; }
    [Id(17)] public DateTime CreatedAt { get; set; }
    [Id(18)] public List<string> PostLogoutRedirectUris { get; set; } = [];
    [Id(19)] public UserFlowSettings UserFlow { get; set; } = new();

    /// <summary>
    /// Application-defined permissions that can be assigned via roles.
    /// </summary>
    [Id(20)] public List<ApplicationPermission> ApplicationPermissions { get; set; } = [];

    /// <summary>
    /// Application-defined roles containing sets of permissions.
    /// </summary>
    [Id(21)] public List<ApplicationRole> ApplicationRoles { get; set; } = [];

    /// <summary>
    /// Whether to include application permissions in access tokens.
    /// </summary>
    [Id(22)] public bool IncludePermissionsInToken { get; set; } = true;

    /// <summary>
    /// The default role ID to assign to new users. Null means no default role.
    /// </summary>
    [Id(23)] public string? DefaultApplicationRoleId { get; set; }
}

[GenerateSerializer]
public sealed class ClientSecret
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public string Description { get; set; } = string.Empty;
    [Id(2)] public string SecretHash { get; set; } = string.Empty;
    [Id(3)] public DateTime CreatedAt { get; set; }
    [Id(4)] public DateTime? ExpiresAt { get; set; }
}

public static class GrantTypes
{
    public const string AuthorizationCode = "authorization_code";
    public const string ClientCredentials = "client_credentials";
    public const string RefreshToken = "refresh_token";
}

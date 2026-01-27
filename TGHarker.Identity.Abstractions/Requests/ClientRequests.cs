namespace TGHarker.Identity.Abstractions.Requests;

[GenerateSerializer]
public sealed class CreateClientRequest
{
    [Id(0)] public string TenantId { get; set; } = string.Empty;
    [Id(1)] public string ClientId { get; set; } = string.Empty;
    [Id(2)] public string? ClientName { get; set; }
    [Id(3)] public string? Description { get; set; }
    [Id(4)] public bool IsConfidential { get; set; }
    [Id(5)] public bool RequireConsent { get; set; }
    [Id(6)] public bool RequirePkce { get; set; } = true;
    [Id(7)] public List<string> RedirectUris { get; set; } = [];
    [Id(8)] public List<string> AllowedScopes { get; set; } = [];
    [Id(9)] public List<string> AllowedGrantTypes { get; set; } = [];
    [Id(10)] public List<string> CorsOrigins { get; set; } = [];
    [Id(11)] public List<string> PostLogoutRedirectUris { get; set; } = [];
}

[GenerateSerializer]
public sealed class UpdateClientRequest
{
    [Id(0)] public string? ClientName { get; set; }
    [Id(1)] public string? Description { get; set; }
    [Id(2)] public bool? RequireConsent { get; set; }
    [Id(3)] public bool? RequirePkce { get; set; }
    [Id(4)] public List<string>? RedirectUris { get; set; }
    [Id(5)] public List<string>? AllowedScopes { get; set; }
    [Id(6)] public List<string>? AllowedGrantTypes { get; set; }
    [Id(7)] public List<string>? CorsOrigins { get; set; }
    [Id(8)] public int? AccessTokenLifetimeMinutes { get; set; }
    [Id(9)] public int? RefreshTokenLifetimeDays { get; set; }
    [Id(10)] public List<string>? PostLogoutRedirectUris { get; set; }
}

[GenerateSerializer]
public sealed class CreateClientResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? ClientSecret { get; set; }
    [Id(2)] public string? Error { get; set; }
}

[GenerateSerializer]
public sealed class AddSecretResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? SecretId { get; set; }
    [Id(2)] public string? PlainTextSecret { get; set; }
    [Id(3)] public string? Error { get; set; }
}

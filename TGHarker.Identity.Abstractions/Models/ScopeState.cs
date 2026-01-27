namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
public sealed class ScopeState
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public string TenantId { get; set; } = string.Empty;
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string? DisplayName { get; set; }
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public bool IsStandard { get; set; }
    [Id(6)] public bool IsRequired { get; set; }
    [Id(7)] public bool ShowInDiscoveryDocument { get; set; } = true;
    [Id(8)] public ScopeType Type { get; set; }
    [Id(9)] public List<ScopeClaim> Claims { get; set; } = [];
    [Id(10)] public DateTime CreatedAt { get; set; }
}

[GenerateSerializer]
public sealed class ScopeClaim
{
    [Id(0)] public string ClaimType { get; set; } = string.Empty;
    [Id(1)] public bool AlwaysInclude { get; set; }
}

public enum ScopeType
{
    Identity,
    Resource
}

public static class StandardScopes
{
    public const string OpenId = "openid";
    public const string Profile = "profile";
    public const string Email = "email";
    public const string Phone = "phone";
    public const string Address = "address";
    public const string OfflineAccess = "offline_access";
}

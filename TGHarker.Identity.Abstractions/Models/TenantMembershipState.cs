namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
public sealed class TenantMembershipState
{
    [Id(0)] public string UserId { get; set; } = string.Empty;
    [Id(1)] public string TenantId { get; set; } = string.Empty;
    [Id(2)] public string? Username { get; set; }
    [Id(3)] public bool IsActive { get; set; }
    [Id(4)] public List<string> Roles { get; set; } = [];
    [Id(5)] public List<UserClaim> Claims { get; set; } = [];
    [Id(6)] public DateTime JoinedAt { get; set; }
    [Id(7)] public DateTime? LastAccessAt { get; set; }
}

[GenerateSerializer]
public sealed class UserClaim
{
    [Id(0)] public string Type { get; set; } = string.Empty;
    [Id(1)] public string Value { get; set; } = string.Empty;
}

public static class WellKnownRoles
{
    public const string TenantAdmin = "tenant_admin";
    public const string TenantOwner = "tenant_owner";
    public const string User = "user";
}

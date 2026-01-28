using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
[Searchable(typeof(IOrganizationGrain))]
public sealed class OrganizationState
{
    [Id(0)] public string Id { get; set; } = string.Empty;

    [Id(1)]
    [Queryable(Indexed = true)]
    public string TenantId { get; set; } = string.Empty;

    [Id(2)]
    [Queryable(Indexed = true)]
    public string Identifier { get; set; } = string.Empty;

    [Id(3)]
    [Queryable]
    public string Name { get; set; } = string.Empty;

    [Id(4)] public string? DisplayName { get; set; }
    [Id(5)] public string? Description { get; set; }

    [Id(6)]
    [Queryable]
    public bool IsActive { get; set; }

    [Id(7)] public OrganizationSettings Settings { get; set; } = new();
    [Id(8)] public List<string> MemberUserIds { get; set; } = [];
    [Id(9)] public List<OrganizationRole> Roles { get; set; } = [];
    [Id(10)] public DateTime CreatedAt { get; set; }
    [Id(11)] public string? CreatedByUserId { get; set; }
}

[GenerateSerializer]
public sealed class OrganizationSettings
{
    [Id(0)] public bool AllowSelfRegistration { get; set; } = false;
    [Id(1)] public bool RequireEmailVerification { get; set; } = true;
    [Id(2)] public string? DefaultRoleId { get; set; }
}

[GenerateSerializer]
public sealed class OrganizationRole
{
    [Id(0)] public string Id { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public string? Description { get; set; }
    [Id(3)] public bool IsSystem { get; set; }
    [Id(4)] public List<string> Permissions { get; set; } = [];
    [Id(5)] public DateTime CreatedAt { get; set; }
}

public static class WellKnownOrganizationRoles
{
    public const string Owner = "org_owner";
    public const string Admin = "org_admin";
    public const string Member = "org_member";
}

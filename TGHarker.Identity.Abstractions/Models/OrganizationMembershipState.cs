using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
[Searchable(typeof(IOrganizationMembershipGrain))]
public sealed class OrganizationMembershipState
{
    [Id(0)]
    [Queryable(Indexed = true)]
    public string UserId { get; set; } = string.Empty;

    [Id(1)]
    [Queryable(Indexed = true)]
    public string OrganizationId { get; set; } = string.Empty;

    [Id(2)]
    [Queryable(Indexed = true)]
    public string TenantId { get; set; } = string.Empty;

    [Id(3)] public string? DisplayName { get; set; }

    [Id(4)]
    [Queryable]
    public bool IsActive { get; set; }

    [Id(5)] public List<string> Roles { get; set; } = [];
    [Id(6)] public List<UserClaim> Claims { get; set; } = [];
    [Id(7)] public DateTime JoinedAt { get; set; }
    [Id(8)] public DateTime? LastAccessAt { get; set; }
}

[GenerateSerializer]
public sealed class OrganizationMembershipRef
{
    [Id(0)] public string TenantId { get; set; } = string.Empty;
    [Id(1)] public string OrganizationId { get; set; } = string.Empty;
}

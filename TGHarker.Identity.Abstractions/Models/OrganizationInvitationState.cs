using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
[Searchable(typeof(IOrganizationInvitationGrain))]
public sealed class OrganizationInvitationState
{
    [Id(0)] public string Id { get; set; } = string.Empty;

    [Id(1)]
    [Queryable(Indexed = true)]
    public string TenantId { get; set; } = string.Empty;

    [Id(2)]
    [Queryable(Indexed = true)]
    public string OrganizationId { get; set; } = string.Empty;

    [Id(3)]
    [Queryable(Indexed = true)]
    public string Email { get; set; } = string.Empty;

    [Id(4)]
    [Queryable(Indexed = true)]
    public string Token { get; set; } = string.Empty;

    [Id(5)] public string InvitedByUserId { get; set; } = string.Empty;
    [Id(6)] public List<string> Roles { get; set; } = [];
    [Id(7)] public DateTime CreatedAt { get; set; }
    [Id(8)] public DateTime ExpiresAt { get; set; }

    [Id(9)]
    [Queryable]
    public InvitationStatus Status { get; set; }

    [Id(10)] public string? AcceptedByUserId { get; set; }
    [Id(11)] public DateTime? AcceptedAt { get; set; }
}

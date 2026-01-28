using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
[Searchable(typeof(IInvitationGrain))]
public sealed class InvitationState
{
    [Id(0)] public string Id { get; set; } = string.Empty;

    [Id(1)]
    [Queryable(Indexed = true)]
    public string TenantId { get; set; } = string.Empty;

    [Id(2)]
    [Queryable(Indexed = true)]
    public string Email { get; set; } = string.Empty;

    [Id(3)]
    [Queryable(Indexed = true)]
    public string Token { get; set; } = string.Empty;

    [Id(4)] public string InvitedByUserId { get; set; } = string.Empty;
    [Id(5)] public List<string> Roles { get; set; } = [];
    [Id(6)] public DateTime CreatedAt { get; set; }
    [Id(7)] public DateTime ExpiresAt { get; set; }

    [Id(8)]
    [Queryable]
    public InvitationStatus Status { get; set; }

    [Id(9)] public string? AcceptedByUserId { get; set; }
    [Id(10)] public DateTime? AcceptedAt { get; set; }
}

[GenerateSerializer]
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2,
    Expired = 3
}

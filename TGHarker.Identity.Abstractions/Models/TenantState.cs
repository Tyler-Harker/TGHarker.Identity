using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
[Searchable(typeof(ITenantGrain))]
public sealed class TenantState
{
    [Id(0)] public string Id { get; set; } = string.Empty;

    [Id(1)]
    [Queryable(Indexed = true)]
    public string Identifier { get; set; } = string.Empty;

    [Id(2)]
    [Queryable]
    public string Name { get; set; } = string.Empty;

    [Id(3)] public string? DisplayName { get; set; }

    [Id(4)]
    [Queryable]
    public bool IsActive { get; set; }

    [Id(5)] public TenantConfiguration Configuration { get; set; } = new();
    [Id(6)] public List<string> MemberUserIds { get; set; } = [];
    [Id(7)] public DateTime CreatedAt { get; set; }
    [Id(8)] public string? CreatedByUserId { get; set; }
    [Id(9)] public List<string> ClientIds { get; set; } = [];
    [Id(10)] public List<TenantRole> Roles { get; set; } = [];
    [Id(11)] public List<string> OrganizationIds { get; set; } = [];
}

[GenerateSerializer]
public sealed class TenantConfiguration
{
    [Id(0)] public int AccessTokenLifetimeMinutes { get; set; } = 60;
    [Id(1)] public int RefreshTokenLifetimeDays { get; set; } = 30;
    [Id(2)] public int AuthorizationCodeLifetimeMinutes { get; set; } = 5;
    [Id(3)] public bool RequirePkce { get; set; } = true;
    [Id(4)] public int MaxLoginAttemptsPerMinute { get; set; } = 5;
    [Id(5)] public int IdTokenLifetimeMinutes { get; set; } = 60;
    [Id(6)] public TenantBranding Branding { get; set; } = new();
}

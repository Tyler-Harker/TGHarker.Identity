using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Abstractions.Attributes;

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
[Searchable(typeof(IUserGrain))]
public sealed class UserState
{
    [Id(0)] public string Id { get; set; } = string.Empty;

    [Id(1)]
    [Queryable(Indexed = true)]
    public string Email { get; set; } = string.Empty;

    [Id(2)] public bool EmailVerified { get; set; }
    [Id(3)] public string PasswordHash { get; set; } = string.Empty;
    [Id(4)] public string? GivenName { get; set; }
    [Id(5)] public string? FamilyName { get; set; }
    [Id(6)] public string? PhoneNumber { get; set; }
    [Id(7)] public bool PhoneNumberVerified { get; set; }
    [Id(8)] public string? Picture { get; set; }

    [Id(9)]
    [Queryable]
    public bool IsActive { get; set; }

    [Id(10)] public bool IsLocked { get; set; }
    [Id(11)] public DateTime? LockoutEnd { get; set; }
    [Id(12)] public int FailedLoginAttempts { get; set; }
    [Id(13)] public DateTime CreatedAt { get; set; }
    [Id(14)] public DateTime? LastLoginAt { get; set; }
    [Id(15)] public List<string> TenantMemberships { get; set; } = [];
    [Id(16)] public string? PasswordResetToken { get; set; }
    [Id(17)] public DateTime? PasswordResetTokenExpiry { get; set; }
    [Id(18)] public string? EmailVerificationToken { get; set; }
    [Id(19)] public DateTime? EmailVerificationTokenExpiry { get; set; }
}

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
public sealed class RefreshTokenState
{
    [Id(0)] public string TenantId { get; set; } = string.Empty;
    [Id(1)] public string ClientId { get; set; } = string.Empty;
    [Id(2)] public string? UserId { get; set; }
    [Id(3)] public List<string> Scopes { get; set; } = [];
    [Id(4)] public DateTime CreatedAt { get; set; }
    [Id(5)] public DateTime ExpiresAt { get; set; }
    [Id(6)] public bool IsRevoked { get; set; }
    [Id(7)] public DateTime? RevokedAt { get; set; }
    [Id(8)] public string? ClientIp { get; set; }
    [Id(9)] public string? UserAgent { get; set; }
    [Id(10)] public string? ReplacedByTokenHash { get; set; }
}

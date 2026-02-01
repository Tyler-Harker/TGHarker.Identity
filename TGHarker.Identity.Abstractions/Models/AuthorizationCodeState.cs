namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
public sealed class AuthorizationCodeState
{
    [Id(0)] public string TenantId { get; set; } = string.Empty;
    [Id(1)] public string ClientId { get; set; } = string.Empty;
    [Id(2)] public string UserId { get; set; } = string.Empty;
    [Id(3)] public string RedirectUri { get; set; } = string.Empty;
    [Id(4)] public List<string> Scopes { get; set; } = [];
    [Id(5)] public string? CodeChallenge { get; set; }
    [Id(6)] public string? CodeChallengeMethod { get; set; }
    [Id(7)] public string? Nonce { get; set; }
    [Id(8)] public string? State { get; set; }
    [Id(9)] public DateTime CreatedAt { get; set; }
    [Id(10)] public DateTime ExpiresAt { get; set; }
    [Id(11)] public bool IsRedeemed { get; set; }
    [Id(12)] public DateTime? RedeemedAt { get; set; }
    [Id(13)] public string? SelectedOrganizationId { get; set; }
}

namespace TGHarker.Identity.Abstractions.Models;

[GenerateSerializer]
public sealed class SigningKeyState
{
    [Id(0)] public string TenantId { get; set; } = string.Empty;
    [Id(1)] public List<SigningKey> Keys { get; set; } = [];
}

[GenerateSerializer]
public sealed class SigningKey
{
    [Id(0)] public string KeyId { get; set; } = string.Empty;
    [Id(1)] public string Algorithm { get; set; } = "RS256";
    [Id(2)] public string PrivateKeyPem { get; set; } = string.Empty;
    [Id(3)] public string PublicKeyPem { get; set; } = string.Empty;
    [Id(4)] public bool IsActive { get; set; }
    [Id(5)] public DateTime CreatedAt { get; set; }
    [Id(6)] public DateTime? ExpiresAt { get; set; }
    [Id(7)] public DateTime? RevokedAt { get; set; }
}

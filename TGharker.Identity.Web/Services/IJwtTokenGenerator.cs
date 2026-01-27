namespace TGharker.Identity.Web.Services;

public interface IJwtTokenGenerator
{
    Task<string> GenerateAccessTokenAsync(TokenGenerationContext context);
    Task<string> GenerateIdTokenAsync(TokenGenerationContext context);
}

public sealed class TokenGenerationContext
{
    public required string TenantId { get; init; }
    public required string Issuer { get; init; }
    public required string Subject { get; init; }
    public required string ClientId { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }
    public string? Audience { get; init; }
    public string? Nonce { get; init; }
    public DateTime? AuthTime { get; init; }
    public int AccessTokenLifetimeMinutes { get; init; } = 60;
    public int IdTokenLifetimeMinutes { get; init; } = 60;
    public Dictionary<string, string> AdditionalClaims { get; init; } = [];
    public Dictionary<string, string> IdentityClaims { get; init; } = [];
}

using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Abstractions.Requests;

[GenerateSerializer]
public sealed class CreateTenantRequest
{
    [Id(0)] public string Identifier { get; set; } = string.Empty;
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public string? DisplayName { get; set; }
    [Id(3)] public string CreatorUserId { get; set; } = string.Empty;
    [Id(4)] public TenantConfiguration? Configuration { get; set; }
}

[GenerateSerializer]
public sealed class CreateTenantResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? TenantId { get; set; }
    [Id(2)] public string? Error { get; set; }
}

namespace TGHarker.Identity.Abstractions.Requests;

[GenerateSerializer]
public sealed class CreateMembershipRequest
{
    [Id(0)] public string UserId { get; set; } = string.Empty;
    [Id(1)] public string TenantId { get; set; } = string.Empty;
    [Id(2)] public string? Username { get; set; }
    [Id(3)] public List<string> Roles { get; set; } = [];
}

[GenerateSerializer]
public sealed class UpdateMembershipRequest
{
    [Id(0)] public string? Username { get; set; }
}

[GenerateSerializer]
public sealed class CreateMembershipResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? Error { get; set; }
}

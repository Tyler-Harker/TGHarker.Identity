namespace TGHarker.Identity.Abstractions.Requests;

[GenerateSerializer]
public sealed class CreateInvitationRequest
{
    [Id(0)] public string TenantId { get; set; } = string.Empty;
    [Id(1)] public string Email { get; set; } = string.Empty;
    [Id(2)] public string InvitedByUserId { get; set; } = string.Empty;
    [Id(3)] public List<string> Roles { get; set; } = [];
}

[GenerateSerializer]
public sealed class CreateInvitationResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? InvitationId { get; set; }
    [Id(2)] public string? Token { get; set; }
    [Id(3)] public string? Error { get; set; }
}

[GenerateSerializer]
public sealed class AcceptInvitationResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? TenantId { get; set; }
    [Id(2)] public string? Error { get; set; }
}

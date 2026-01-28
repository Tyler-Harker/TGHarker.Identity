using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Abstractions.Requests;

[GenerateSerializer]
public sealed class CreateOrganizationRequest
{
    [Id(0)] public string TenantId { get; set; } = string.Empty;
    [Id(1)] public string Identifier { get; set; } = string.Empty;
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string? DisplayName { get; set; }
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public string CreatorUserId { get; set; } = string.Empty;
    [Id(6)] public OrganizationSettings? Settings { get; set; }
}

[GenerateSerializer]
public sealed class CreateOrganizationResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? OrganizationId { get; set; }
    [Id(2)] public string? Error { get; set; }
}

[GenerateSerializer]
public sealed class UpdateOrganizationRequest
{
    [Id(0)] public string? Name { get; set; }
    [Id(1)] public string? DisplayName { get; set; }
    [Id(2)] public string? Description { get; set; }
    [Id(3)] public OrganizationSettings? Settings { get; set; }
}

[GenerateSerializer]
public sealed class CreateOrganizationMembershipRequest
{
    [Id(0)] public string UserId { get; set; } = string.Empty;
    [Id(1)] public string OrganizationId { get; set; } = string.Empty;
    [Id(2)] public string TenantId { get; set; } = string.Empty;
    [Id(3)] public string? DisplayName { get; set; }
    [Id(4)] public List<string> Roles { get; set; } = [];
}

[GenerateSerializer]
public sealed class CreateOrganizationMembershipResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? Error { get; set; }
}

[GenerateSerializer]
public sealed class CreateOrganizationInvitationRequest
{
    [Id(0)] public string TenantId { get; set; } = string.Empty;
    [Id(1)] public string OrganizationId { get; set; } = string.Empty;
    [Id(2)] public string Email { get; set; } = string.Empty;
    [Id(3)] public string InvitedByUserId { get; set; } = string.Empty;
    [Id(4)] public List<string> Roles { get; set; } = [];
}

[GenerateSerializer]
public sealed class CreateOrganizationInvitationResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? InvitationId { get; set; }
    [Id(2)] public string? Token { get; set; }
    [Id(3)] public string? Error { get; set; }
}

[GenerateSerializer]
public sealed class AcceptOrganizationInvitationResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? TenantId { get; set; }
    [Id(2)] public string? OrganizationId { get; set; }
    [Id(3)] public string? Error { get; set; }
}

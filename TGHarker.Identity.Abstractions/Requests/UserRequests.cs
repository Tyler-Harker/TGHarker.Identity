namespace TGHarker.Identity.Abstractions.Requests;

[GenerateSerializer]
public sealed class CreateUserRequest
{
    [Id(0)] public string Email { get; set; } = string.Empty;
    [Id(1)] public string Password { get; set; } = string.Empty;
    [Id(2)] public string? GivenName { get; set; }
    [Id(3)] public string? FamilyName { get; set; }
    [Id(4)] public string? PhoneNumber { get; set; }
}

[GenerateSerializer]
public sealed class CreateUserResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? UserId { get; set; }
    [Id(2)] public string? Error { get; set; }
}

[GenerateSerializer]
public sealed class UpdateProfileRequest
{
    [Id(0)] public string? GivenName { get; set; }
    [Id(1)] public string? FamilyName { get; set; }
    [Id(2)] public string? PhoneNumber { get; set; }
    [Id(3)] public string? Picture { get; set; }
}

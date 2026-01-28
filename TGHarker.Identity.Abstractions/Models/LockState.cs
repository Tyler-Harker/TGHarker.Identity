namespace TGHarker.Identity.Abstractions.Models;

public enum LockStatus
{
    Unlocked,
    Locked
}

[GenerateSerializer]
public sealed class LockState<TOwner>
{
    [Id(0)] public LockStatus Status { get; set; } = LockStatus.Unlocked;
    [Id(1)] public TOwner? Owner { get; set; }
    [Id(2)] public DateTime? LockedAt { get; set; }
}

[GenerateSerializer]
public sealed class LockResult
{
    [Id(0)] public bool Success { get; set; }
    [Id(1)] public string? Error { get; set; }
    [Id(2)] public string? CurrentOwner { get; set; }

    public static LockResult Acquired() => new() { Success = true };
    public static LockResult AlreadyOwned() => new() { Success = true };
    public static LockResult LockedByOther(string owner) => new()
    {
        Success = false,
        Error = "Resource is locked by another owner",
        CurrentOwner = owner
    };
}

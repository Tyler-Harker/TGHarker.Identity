using Orleans.Runtime;

namespace TGHarker.Identity.Grains;

using TGHarker.Identity.Abstractions.Grains;

[GenerateSerializer]
public sealed class UserRegistryState
{
    [Id(0)] public Dictionary<string, string> EmailToUserId { get; set; } = new();
}

public sealed class UserRegistryGrain : Grain, IUserRegistryGrain
{
    private readonly IPersistentState<UserRegistryState> _state;

    public UserRegistryGrain(
        [PersistentState("userRegistry", "Default")] IPersistentState<UserRegistryState> state)
    {
        _state = state;
    }

    public async Task<bool> RegisterUserAsync(string email, string userId)
    {
        var normalizedEmail = email.ToLowerInvariant();

        if (_state.State.EmailToUserId.ContainsKey(normalizedEmail))
            return false;

        _state.State.EmailToUserId[normalizedEmail] = userId;
        await _state.WriteStateAsync();

        return true;
    }

    public Task<string?> GetUserIdByEmailAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();

        return Task.FromResult(
            _state.State.EmailToUserId.TryGetValue(normalizedEmail, out var userId)
                ? userId
                : null);
    }

    public Task<bool> EmailExistsAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();
        return Task.FromResult(_state.State.EmailToUserId.ContainsKey(normalizedEmail));
    }

    public async Task RemoveUserAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();

        if (_state.State.EmailToUserId.Remove(normalizedEmail))
        {
            await _state.WriteStateAsync();
        }
    }
}

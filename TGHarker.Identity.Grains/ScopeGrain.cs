using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Grains;

public sealed class ScopeGrain : Grain, IScopeGrain
{
    private readonly IPersistentState<ScopeState> _state;

    public ScopeGrain(
        [PersistentState("scope", "Default")] IPersistentState<ScopeState> state)
    {
        _state = state;
    }

    public Task<ScopeState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.Id))
            return Task.FromResult<ScopeState?>(null);

        return Task.FromResult<ScopeState?>(_state.State);
    }

    public async Task<bool> CreateAsync(ScopeState state)
    {
        if (!string.IsNullOrEmpty(_state.State.Id))
            return false;

        _state.State = state;
        await _state.WriteStateAsync();

        return true;
    }

    public async Task UpdateAsync(ScopeState state)
    {
        _state.State = state;
        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_state.State.Id));
    }

    public Task<IReadOnlyList<ScopeClaim>> GetClaimsAsync()
    {
        return Task.FromResult<IReadOnlyList<ScopeClaim>>(_state.State.Claims);
    }
}

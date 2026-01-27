using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;

namespace TGHarker.Identity.Grains;

[GenerateSerializer]
public sealed class TenantRegistryState
{
    [Id(0)] public Dictionary<string, string> IdentifierToTenantId { get; set; } = new();
}

public sealed class TenantRegistryGrain : Grain, ITenantRegistryGrain
{
    private readonly IPersistentState<TenantRegistryState> _state;

    public TenantRegistryGrain(
        [PersistentState("tenantRegistry", "Default")] IPersistentState<TenantRegistryState> state)
    {
        _state = state;
    }

    public async Task<bool> RegisterTenantAsync(string tenantId, string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();

        if (_state.State.IdentifierToTenantId.ContainsKey(normalizedIdentifier))
            return false;

        _state.State.IdentifierToTenantId[normalizedIdentifier] = tenantId;
        await _state.WriteStateAsync();

        return true;
    }

    public Task<string?> GetTenantIdByIdentifierAsync(string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();

        return Task.FromResult(
            _state.State.IdentifierToTenantId.TryGetValue(normalizedIdentifier, out var tenantId)
                ? tenantId
                : null);
    }

    public Task<bool> TenantExistsAsync(string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();
        return Task.FromResult(_state.State.IdentifierToTenantId.ContainsKey(normalizedIdentifier));
    }

    public Task<IReadOnlyList<string>> GetAllTenantIdentifiersAsync()
    {
        return Task.FromResult<IReadOnlyList<string>>(_state.State.IdentifierToTenantId.Keys.ToList());
    }

    public async Task RemoveTenantAsync(string identifier)
    {
        var normalizedIdentifier = identifier.ToLowerInvariant();

        if (_state.State.IdentifierToTenantId.Remove(normalizedIdentifier))
        {
            await _state.WriteStateAsync();
        }
    }
}

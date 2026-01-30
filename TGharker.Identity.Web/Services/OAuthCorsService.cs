using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Services;

public class OAuthCorsService : IOAuthCorsService
{
    private readonly IClusterClient _clusterClient;

    public OAuthCorsService(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public async Task<bool> IsOriginAllowedAsync(string tenantId, string origin)
    {
        var corsGrain = _clusterClient.GetGrain<ICorsOriginsGrain>($"{tenantId}/cors-origins");
        return await corsGrain.IsOriginAllowedAsync(origin);
    }

    public async Task<IReadOnlyList<string>> GetAllowedOriginsAsync(string tenantId)
    {
        var corsGrain = _clusterClient.GetGrain<ICorsOriginsGrain>($"{tenantId}/cors-origins");
        return await corsGrain.GetOriginsAsync();
    }
}

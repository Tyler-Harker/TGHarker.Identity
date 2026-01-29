using System.Web;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Services;

public sealed class UserFlowService : IUserFlowService
{
    private readonly IClusterClient _clusterClient;

    public UserFlowService(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public async Task<UserFlowSettings?> GetUserFlowForClientAsync(string tenantId, string clientId)
    {
        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
            return null;

        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (!await clientGrain.ExistsAsync())
            return null;

        return await clientGrain.GetUserFlowSettingsAsync();
    }

    public async Task<UserFlowSettings?> ResolveUserFlowFromReturnUrlAsync(string tenantId, string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
            return null;

        var clientId = ExtractClientIdFromReturnUrl(returnUrl);
        if (string.IsNullOrEmpty(clientId))
            return null;

        return await GetUserFlowForClientAsync(tenantId, clientId);
    }

    private static string? ExtractClientIdFromReturnUrl(string returnUrl)
    {
        try
        {
            // Handle both absolute and relative URLs
            Uri uri;
            if (returnUrl.StartsWith('/'))
            {
                // Relative URL - add dummy base
                uri = new Uri(new Uri("http://localhost"), returnUrl);
            }
            else
            {
                uri = new Uri(returnUrl, UriKind.Absolute);
            }

            var query = HttpUtility.ParseQueryString(uri.Query);
            return query["client_id"];
        }
        catch
        {
            return null;
        }
    }
}

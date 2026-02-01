using System.Web;
using Microsoft.Extensions.Logging;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Services;

public sealed class UserFlowService : IUserFlowService
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<UserFlowService> _logger;

    public UserFlowService(IClusterClient clusterClient, ILogger<UserFlowService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public async Task<UserFlowSettings?> GetUserFlowForClientAsync(string tenantId, string clientId)
    {
        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
        {
            _logger.LogDebug("GetUserFlowForClientAsync: tenantId or clientId is empty");
            return null;
        }

        var grainKey = $"{tenantId}/{clientId}";
        var clientGrain = _clusterClient.GetGrain<IClientGrain>(grainKey);
        if (!await clientGrain.ExistsAsync())
        {
            _logger.LogWarning("GetUserFlowForClientAsync: Client {GrainKey} does not exist", grainKey);
            return null;
        }

        var userFlow = await clientGrain.GetUserFlowSettingsAsync();
        _logger.LogInformation(
            "GetUserFlowForClientAsync: Client {ClientId} UserFlow - OrganizationsEnabled={OrganizationsEnabled}, Mode={Mode}",
            clientId, userFlow.OrganizationsEnabled, userFlow.OrganizationMode);
        return userFlow;
    }

    public async Task<UserFlowSettings?> ResolveUserFlowFromReturnUrlAsync(string tenantId, string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
        {
            _logger.LogDebug("ResolveUserFlowFromReturnUrlAsync: returnUrl is empty");
            return null;
        }

        var clientId = ExtractClientIdFromReturnUrl(returnUrl);
        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogWarning("ResolveUserFlowFromReturnUrlAsync: Could not extract client_id from returnUrl: {ReturnUrl}", returnUrl);
            return null;
        }

        _logger.LogDebug("ResolveUserFlowFromReturnUrlAsync: Extracted client_id={ClientId} from returnUrl", clientId);
        return await GetUserFlowForClientAsync(tenantId, clientId);
    }

    private static string? ExtractClientIdFromReturnUrl(string returnUrl)
    {
        try
        {
            // The returnUrl may be URL-encoded (e.g., from query string), so decode it first
            var decodedUrl = HttpUtility.UrlDecode(returnUrl);

            // Handle both absolute and relative URLs
            Uri uri;
            if (decodedUrl.StartsWith('/'))
            {
                // Relative URL - add dummy base
                uri = new Uri(new Uri("http://localhost"), decodedUrl);
            }
            else
            {
                uri = new Uri(decodedUrl, UriKind.Absolute);
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

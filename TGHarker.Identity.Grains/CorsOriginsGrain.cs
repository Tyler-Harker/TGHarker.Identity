using Microsoft.Extensions.Logging;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGHarker.Identity.Grains;

/// <summary>
/// Grain that caches allowed CORS origins for a tenant.
/// This grain does not persist state - it rebuilds from clients on activation.
/// Origins are extracted from:
/// - Client CorsOrigins (explicit CORS origins)
/// - Client RedirectUris (OAuth redirect endpoints)
/// - Client PostLogoutRedirectUris (post-logout redirect endpoints)
/// </summary>
public sealed class CorsOriginsGrain : Grain, ICorsOriginsGrain
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<CorsOriginsGrain> _logger;
    private HashSet<string> _origins = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public CorsOriginsGrain(IClusterClient clusterClient, ILogger<CorsOriginsGrain> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Don't refresh on activation - use lazy loading to avoid deadlocks
        // when ClientGrain calls InvalidateAsync during its own update
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetOriginsAsync()
    {
        if (!_initialized)
        {
            await RefreshOriginsAsync();
        }
        return _origins.ToList();
    }

    public async Task<bool> IsOriginAllowedAsync(string origin)
    {
        if (!_initialized)
        {
            await RefreshOriginsAsync();
        }

        var normalizedOrigin = NormalizeOrigin(origin);
        return _origins.Any(o => NormalizeOrigin(o) == normalizedOrigin);
    }

    public Task InvalidateAsync()
    {
        _logger.LogDebug("CORS origins cache invalidated for tenant");
        _initialized = false;
        _origins.Clear();
        return Task.CompletedTask;
    }

    private async Task RefreshOriginsAsync()
    {
        var tenantId = GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
        {
            _logger.LogWarning("Invalid grain key, cannot determine tenant ID");
            _initialized = true;
            return;
        }

        try
        {
            var clients = await _clusterClient.Search<IClientGrain>()
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .ToListAsync();

            var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var client in clients)
            {
                var state = await client.GetStateAsync();
                if (state == null)
                    continue;

                // Add explicit CORS origins
                if (state.CorsOrigins != null)
                {
                    foreach (var corsOrigin in state.CorsOrigins)
                    {
                        origins.Add(corsOrigin);
                    }
                }

                // Add origins extracted from redirect URIs
                if (state.RedirectUris != null)
                {
                    foreach (var redirectUri in state.RedirectUris)
                    {
                        var origin = ExtractOriginFromUrl(redirectUri);
                        if (origin != null)
                        {
                            origins.Add(origin);
                        }
                    }
                }

                // Add origins extracted from post-logout redirect URIs
                if (state.PostLogoutRedirectUris != null)
                {
                    foreach (var postLogoutUri in state.PostLogoutRedirectUris)
                    {
                        var origin = ExtractOriginFromUrl(postLogoutUri);
                        if (origin != null)
                        {
                            origins.Add(origin);
                        }
                    }
                }
            }

            _origins = origins;
            _initialized = true;

            _logger.LogDebug("Loaded {Count} CORS origins for tenant {TenantId} (from CorsOrigins + RedirectUris + PostLogoutRedirectUris)", origins.Count, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh CORS origins for tenant {TenantId}", tenantId);
            _initialized = true;
        }
    }

    private string GetTenantId()
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split('/');
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private static string? ExtractOriginFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}";
        }
        return null;
    }

    private static string NormalizeOrigin(string origin)
    {
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}".ToLowerInvariant();
        }
        return origin.ToLowerInvariant().TrimEnd('/');
    }
}

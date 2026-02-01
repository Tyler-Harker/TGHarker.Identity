using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Services;

public sealed class TenantResolver : ITenantResolver
{
    private readonly IGrainSearchService _searchService;

    public TenantResolver(IGrainSearchService searchService)
    {
        _searchService = searchService;
    }

    public async Task<TenantState?> ResolveAsync(HttpContext context)
    {
        var identifier = GetTenantIdentifier(context);
        if (string.IsNullOrEmpty(identifier))
            return null;

        // Get tenant using search
        var tenantGrain = await _searchService.GetTenantByIdentifierAsync(identifier);
        if (tenantGrain == null)
            return null;

        return await tenantGrain.GetStateAsync();
    }

    public string? GetTenantIdentifier(HttpContext context)
    {
        // Strategy 1: X-Tenant-Id header
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTenant))
        {
            return headerTenant.ToString();
        }

        // Strategy 2: Subdomain (e.g., acme.identity.example.com)
        var host = context.Request.Host.Host;
        var subdomain = ExtractSubdomain(host);
        if (!string.IsNullOrEmpty(subdomain))
        {
            return subdomain;
        }

        // Strategy 3: Path segment (e.g., /tenants/{tenantId}/connect/token)
        var pathTenant = ExtractTenantFromPath(context.Request.Path);
        if (!string.IsNullOrEmpty(pathTenant))
        {
            return pathTenant;
        }

        // Strategy 4: Query parameter
        if (context.Request.Query.TryGetValue("tenant", out var queryTenant))
        {
            return queryTenant.ToString();
        }

        return null;
    }

    private static string? ExtractSubdomain(string host)
    {
        // Remove port if present
        var hostWithoutPort = host.Split(':')[0];
        var parts = hostWithoutPort.Split('.');

        // Need at least 3 parts for subdomain (e.g., acme.identity.com)
        if (parts.Length > 2)
        {
            var subdomain = parts[0];

            // Skip common non-tenant subdomains
            if (subdomain is "www" or "api" or "localhost")
                return null;

            return subdomain;
        }

        return null;
    }

    private static readonly HashSet<string> KnownRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".well-known", "connect", "account", "admin", "docs", "stats", "tenants", "tenant", "api"
    };

    private static string? ExtractTenantFromPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments == null || segments.Length == 0)
            return null;

        // Strategy A: /tenants/{tenantId}/... (legacy format)
        if (segments is { Length: > 1 } && segments[0] == "tenants")
        {
            return segments[1];
        }

        // Strategy B: /{tenantId}/... where first segment is not a known route
        if (segments.Length >= 1 && !KnownRoutes.Contains(segments[0]))
        {
            return segments[0];
        }

        return null;
    }
}

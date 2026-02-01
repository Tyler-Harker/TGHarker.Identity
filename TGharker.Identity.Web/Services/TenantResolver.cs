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
        
        // Strategy 2: Path segment (e.g., /tenants/{tenantId}/connect/token)
        var pathTenant = ExtractTenantFromPath(context.Request.Path);
        if (!string.IsNullOrEmpty(pathTenant))
        {
            return pathTenant;
        }

        // Strategy 3: Query parameter
        if (context.Request.Query.TryGetValue("tenant", out var queryTenant))
        {
            return queryTenant.ToString();
        }

        // Strategy 4: Subdomain (e.g., acme.identity.example.com)
        var host = context.Request.Host.Host;
        var subdomain = ExtractSubdomain(host);
        if (!string.IsNullOrEmpty(subdomain))
        {
            return subdomain;
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

        // Strategy A: /tenant/{tenantId}/... (new preferred format)
        if (segments.Length >= 2 && segments[0].Equals("tenant", StringComparison.OrdinalIgnoreCase))
        {
            return segments[1];
        }

        // Strategy B: /tenants/{tenantId}/... (legacy format)
        if (segments.Length >= 2 && segments[0].Equals("tenants", StringComparison.OrdinalIgnoreCase))
        {
            return segments[1];
        }

        return null;
    }

    public string GetIssuerUrl(HttpContext context, TenantState tenant)
    {
        var scheme = context.Request.Scheme;
        var host = context.Request.Host;

        // Check if tenant was resolved via subdomain
        var subdomain = ExtractSubdomain(host.Host);
        if (!string.IsNullOrEmpty(subdomain) && subdomain.Equals(tenant.Identifier, StringComparison.OrdinalIgnoreCase))
        {
            // Subdomain-based: issuer is just the host (no path prefix)
            return $"{scheme}://{host}";
        }

        // Path-based: always use /tenant/{identifier} format for consistency
        return $"{scheme}://{host}/tenant/{tenant.Identifier}";
    }
}

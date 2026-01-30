using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Endpoints;

public static class StatsEndpoint
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string CacheKey = "platform_stats";

    public static IEndpointRouteBuilder MapStatsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/stats", GetStatsAsync)
            .AllowAnonymous()
            .WithName("GetPlatformStats")
            .WithTags("Stats")
            .Produces<StatsResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> GetStatsAsync(
        IGrainSearchService searchService,
        IMemoryCache cache)
    {
        // Try to get from cache first
        if (cache.TryGetValue(CacheKey, out StatsResponse? cachedStats) && cachedStats != null)
        {
            return Results.Ok(cachedStats);
        }

        // Fetch fresh stats
        var stats = await searchService.GetPlatformStatsAsync();

        var response = new StatsResponse
        {
            TotalUsers = stats.TotalUsers,
            TotalTenants = stats.TotalTenants,
            MonthlyActiveUsers = stats.MonthlyActiveUsers,
            GeneratedAt = stats.GeneratedAt
        };

        // Cache the results
        cache.Set(CacheKey, response, CacheDuration);

        return Results.Ok(response);
    }
}

public sealed class StatsResponse
{
    [JsonPropertyName("total_users")]
    public long TotalUsers { get; set; }

    [JsonPropertyName("total_tenants")]
    public long TotalTenants { get; set; }

    [JsonPropertyName("monthly_active_users")]
    public long MonthlyActiveUsers { get; set; }

    [JsonPropertyName("generated_at")]
    public DateTime GeneratedAt { get; set; }
}

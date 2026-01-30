using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages;

public class StatsModel : PageModel
{
    private readonly IGrainSearchService _searchService;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string CacheKey = "platform_stats_page";

    public PlatformStats Stats { get; private set; } = new();

    public StatsModel(IGrainSearchService searchService, IMemoryCache cache)
    {
        _searchService = searchService;
        _cache = cache;
    }

    public async Task OnGetAsync()
    {
        // Try cache first
        if (_cache.TryGetValue(CacheKey, out PlatformStats? cachedStats) && cachedStats != null)
        {
            Stats = cachedStats;
            return;
        }

        // Fetch fresh stats
        Stats = await _searchService.GetPlatformStatsAsync();

        // Cache the results
        _cache.Set(CacheKey, Stats, CacheDuration);
    }
}

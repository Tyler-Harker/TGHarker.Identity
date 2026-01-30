using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IGrainSearchService _searchService;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const string CacheKey = "platform_stats_home";

    public PlatformStats Stats { get; private set; } = new();

    public IndexModel(IGrainSearchService searchService, IMemoryCache cache)
    {
        _searchService = searchService;
        _cache = cache;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // If user is authenticated with a tenant, redirect to dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(tenantId))
            {
                return RedirectToPage("/Dashboard/Index");
            }
        }

        // Load stats for the home page
        if (_cache.TryGetValue(CacheKey, out PlatformStats? cachedStats) && cachedStats != null)
        {
            Stats = cachedStats;
        }
        else
        {
            Stats = await _searchService.GetPlatformStatsAsync();
            _cache.Set(CacheKey, Stats, CacheDuration);
        }

        return Page();
    }
}

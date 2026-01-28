using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGharker.Identity.Web.Services;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Pages.Tenants;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ISessionService _sessionService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IClusterClient clusterClient,
        ISessionService sessionService,
        ILogger<IndexModel> logger)
    {
        _clusterClient = clusterClient;
        _sessionService = sessionService;
        _logger = logger;
    }

    public List<TenantInfo> Tenants { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public class TenantInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID not found in claims");
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Check if user already has a tenant selected (full session)
        var existingTenantId = User.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(existingTenantId))
        {
            // User already has a tenant, redirect to app
            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }
            return RedirectToPage("/Index");
        }

        await LoadTenantsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSelectAsync(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            ErrorMessage = "No tenant selected.";
            await LoadTenantsAsync();
            return Page();
        }

        var userId = GetUserId();

        // Verify user is a member of this tenant
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var memberships = await userGrain.GetTenantMembershipsAsync();

        if (!memberships.Contains(tenantId))
        {
            ErrorMessage = "You are not a member of this tenant.";
            await LoadTenantsAsync();
            return Page();
        }

        // Get user state for completing session
        var user = await userGrain.GetStateAsync();
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return RedirectToPage("/Account/Login");
        }

        // Check RememberMe from existing auth properties
        var authResult = await HttpContext.AuthenticateAsync();
        var rememberMe = authResult.Properties?.IsPersistent ?? false;

        try
        {
            await _sessionService.CompleteSessionAsync(HttpContext, userId, user, tenantId, rememberMe);

            _logger.LogInformation("User {UserId} selected tenant {TenantId}", userId, tenantId);

            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing session for user {UserId} with tenant {TenantId}", userId, tenantId);
            ErrorMessage = "An error occurred while selecting the tenant.";
            await LoadTenantsAsync();
            return Page();
        }
    }

    private async Task LoadTenantsAsync()
    {
        var userId = GetUserId();

        // Search for active memberships for this user
        TotalCount = await _clusterClient.Search<ITenantMembershipGrain>()
            .Where(m => m.UserId == userId && m.IsActive)
            .CountAsync();

        var membershipGrains = await _clusterClient.Search<ITenantMembershipGrain>()
            .Where(m => m.UserId == userId && m.IsActive)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Tenants.Clear();
        foreach (var membershipGrain in membershipGrains)
        {
            var membership = await membershipGrain.GetStateAsync();
            if (membership == null) continue;

            var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(membership.TenantId);
            var tenant = await tenantGrain.GetStateAsync();

            if (tenant != null && tenant.IsActive)
            {
                Tenants.Add(new TenantInfo
                {
                    Id = tenant.Id,
                    Identifier = tenant.Identifier,
                    Name = tenant.Name,
                    DisplayName = tenant.DisplayName
                });
            }
        }
    }
}

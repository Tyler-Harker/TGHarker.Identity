using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Dashboard;

[Authorize]
public class SwitchTenantModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ISessionService _sessionService;
    private readonly ILogger<SwitchTenantModel> _logger;

    public SwitchTenantModel(
        IClusterClient clusterClient,
        ISessionService sessionService,
        ILogger<SwitchTenantModel> logger)
    {
        _clusterClient = clusterClient;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(string tenantId, string? returnUrl)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return RedirectToPage("/Dashboard/Index");
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Verify user is a member of this tenant
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var memberships = await userGrain.GetTenantMembershipsAsync();

        if (!memberships.Contains(tenantId))
        {
            _logger.LogWarning("User {UserId} attempted to switch to unauthorized tenant {TenantId}", userId, tenantId);
            return RedirectToPage("/Dashboard/Index");
        }

        // Get user state
        var user = await userGrain.GetStateAsync();
        if (user == null)
        {
            return RedirectToPage("/Account/Login");
        }

        // Check current auth properties for RememberMe
        var authResult = await HttpContext.AuthenticateAsync();
        var rememberMe = authResult.Properties?.IsPersistent ?? false;

        // Complete session with new tenant
        await _sessionService.CompleteSessionAsync(HttpContext, userId, user, tenantId, rememberMe);

        _logger.LogInformation("User {UserId} switched to tenant {TenantId}", userId, tenantId);

        // Redirect back or to dashboard
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToPage("/Dashboard/Index");
    }
}

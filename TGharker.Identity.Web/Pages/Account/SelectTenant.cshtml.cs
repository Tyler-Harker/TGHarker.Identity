using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Pages.Account;

public class SelectTenantModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<SelectTenantModel> _logger;

    public SelectTenantModel(IClusterClient clusterClient, ILogger<SelectTenantModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public List<TenantInfo> Tenants { get; set; } = [];

    public class TenantInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
    }

    public IActionResult OnGetAsync()
    {
        // Redirect to the new unified tenant landing page
        return RedirectToPage("/Tenants/Index", new { returnUrl = ReturnUrl });
    }

    public async Task<IActionResult> OnGetSelectAsync(string tenantId, string userId, string? returnUrl)
    {
        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Get user
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();

        if (user == null)
        {
            return RedirectToPage("/Account/Login");
        }

        // Verify membership
        var memberships = await userGrain.GetTenantMembershipsAsync();
        if (!memberships.Contains(tenantId))
        {
            return RedirectToPage("/Account/Login");
        }

        // Get tenant
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var tenant = await tenantGrain.GetStateAsync();

        if (tenant == null || !tenant.IsActive)
        {
            return RedirectToPage("/Account/Login");
        }

        // Get membership for roles
        var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
        var membership = await membershipGrain.GetStateAsync();

        // Create claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new(ClaimTypes.Email, user.Email),
            new("tenant_id", tenantId),
            new("tenant_identifier", tenant.Identifier)
        };

        if (!string.IsNullOrEmpty(user.GivenName))
            claims.Add(new Claim(ClaimTypes.GivenName, user.GivenName));

        if (!string.IsNullOrEmpty(user.FamilyName))
            claims.Add(new Claim(ClaimTypes.Surname, user.FamilyName));

        // Add roles
        if (membership != null)
        {
            foreach (var role in membership.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        _logger.LogInformation("User {UserId} selected tenant {TenantId}", userId, tenantId);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }
}

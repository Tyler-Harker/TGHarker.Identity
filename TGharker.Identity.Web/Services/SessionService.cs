using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Services;

public interface ISessionService
{
    Task CreatePartialSessionAsync(HttpContext context, string userId, UserState user, bool rememberMe);
    Task CompleteSessionAsync(HttpContext context, string userId, UserState user, string tenantId, bool rememberMe);
}

public class SessionService : ISessionService
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<SessionService> _logger;

    public SessionService(IClusterClient clusterClient, ILogger<SessionService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public async Task CreatePartialSessionAsync(HttpContext context, string userId, UserState user, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new(ClaimTypes.Email, user.Email),
            new("requires_tenant_selection", "true")
        };

        if (!string.IsNullOrEmpty(user.GivenName))
            claims.Add(new Claim(ClaimTypes.GivenName, user.GivenName));

        if (!string.IsNullOrEmpty(user.FamilyName))
            claims.Add(new Claim(ClaimTypes.Surname, user.FamilyName));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        _logger.LogInformation("Created partial session for user {UserId} (requires tenant selection)", userId);
    }

    public async Task CompleteSessionAsync(HttpContext context, string userId, UserState user, string tenantId, bool rememberMe)
    {
        // Get tenant info
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var tenant = await tenantGrain.GetStateAsync();

        if (tenant == null || !tenant.IsActive)
        {
            throw new InvalidOperationException("Tenant not found or inactive");
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
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        _logger.LogInformation("User {UserId} completed session with tenant {TenantId}", userId, tenantId);
    }
}

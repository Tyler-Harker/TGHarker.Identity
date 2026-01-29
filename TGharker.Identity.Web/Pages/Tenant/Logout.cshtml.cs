using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace TGharker.Identity.Web.Pages.Tenant;

public class LogoutModel : TenantAuthPageModel
{
    public LogoutModel(
        IClusterClient clusterClient,
        ILogger<LogoutModel> logger)
        : base(clusterClient, logger)
    {
    }

    public bool LoggedOut { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        var userId = User.FindFirst("sub")?.Value;

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        Logger.LogInformation("User {UserId} logged out from tenant {TenantId}", userId ?? "unknown", TenantId);

        LoggedOut = true;

        // If there's a return URL, redirect there
        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return Page();
    }
}

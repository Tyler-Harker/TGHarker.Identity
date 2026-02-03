using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TGHarker.Identity.ExampleOrganizationWeb.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    public string? AccessToken { get; private set; }
    public string? IdToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public async Task OnGetAsync()
    {
        AccessToken = await HttpContext.GetTokenAsync("access_token");
        IdToken = await HttpContext.GetTokenAsync("id_token");
        RefreshToken = await HttpContext.GetTokenAsync("refresh_token");

        var expiresAtStr = await HttpContext.GetTokenAsync("expires_at");
        if (DateTimeOffset.TryParse(expiresAtStr, out var expiresAt))
        {
            ExpiresAt = expiresAt;
        }
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TGharker.Identity.Web.Pages.Dashboard;

[Authorize]
public class ProfileModel : PageModel
{
    public string Email { get; set; } = string.Empty;
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }

    public void OnGet()
    {
        Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        GivenName = User.FindFirst(ClaimTypes.GivenName)?.Value;
        FamilyName = User.FindFirst(ClaimTypes.Surname)?.Value;
    }
}

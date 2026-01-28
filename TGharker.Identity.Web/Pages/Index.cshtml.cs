using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TGharker.Identity.Web.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
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

        // Otherwise show the home page (or redirect to login)
        return Page();
    }
}

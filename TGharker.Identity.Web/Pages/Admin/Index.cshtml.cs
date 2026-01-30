using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Admin;

[Authorize(AuthenticationSchemes = "SuperAdmin", Policy = "SuperAdmin")]
public class IndexModel : PageModel
{
    private readonly IAdminService _adminService;

    public IndexModel(IAdminService adminService)
    {
        _adminService = adminService;
    }

    public Dictionary<string, int> GrainCounts { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        GrainCounts = await _adminService.GetGrainCountsAsync();
        return Page();
    }
}

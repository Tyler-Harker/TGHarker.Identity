using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Admin;

[Authorize(AuthenticationSchemes = "SuperAdmin", Policy = "SuperAdmin")]
public class GrainDetailsModel : PageModel
{
    private readonly IAdminService _adminService;

    public GrainDetailsModel(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [BindProperty(SupportsGet = true)]
    public string GrainType { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string GrainId { get; set; } = string.Empty;

    public GrainDetail? GrainDetail { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(GrainType) || string.IsNullOrEmpty(GrainId))
        {
            return RedirectToPage("/Admin/Grains");
        }

        GrainDetail = await _adminService.GetGrainStateAsync(GrainType, GrainId);
        return Page();
    }
}

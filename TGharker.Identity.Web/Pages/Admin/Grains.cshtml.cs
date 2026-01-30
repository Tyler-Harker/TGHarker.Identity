using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Admin;

[Authorize(AuthenticationSchemes = "SuperAdmin", Policy = "SuperAdmin")]
public class GrainsModel : PageModel
{
    private readonly IAdminService _adminService;

    public GrainsModel(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [BindProperty(SupportsGet = true)]
    public string SelectedType { get; set; } = "User";

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    public IReadOnlyList<string> GrainTypes { get; private set; } = [];
    public List<GrainSummary> Grains { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        GrainTypes = _adminService.GetSupportedGrainTypes();

        if (!GrainTypes.Contains(SelectedType))
        {
            SelectedType = GrainTypes.FirstOrDefault() ?? "User";
        }

        if (CurrentPage < 1) CurrentPage = 1;
        if (PageSize < 1) PageSize = 25;

        var skip = (CurrentPage - 1) * PageSize;
        Grains = await _adminService.GetGrainsAsync(SelectedType, skip, PageSize);

        return Page();
    }
}

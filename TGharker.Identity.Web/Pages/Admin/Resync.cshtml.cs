using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Admin;

[Authorize(AuthenticationSchemes = "SuperAdmin", Policy = "SuperAdmin")]
public class ResyncModel : PageModel
{
    private readonly IAdminService _adminService;
    private readonly ILogger<ResyncModel> _logger;

    public ResyncModel(IAdminService adminService, ILogger<ResyncModel> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    [BindProperty]
    public string SelectedType { get; set; } = "All";

    public IReadOnlyList<string> GrainTypes { get; private set; } = [];
    public ResyncResult? Result { get; set; }

    public void OnGet()
    {
        GrainTypes = _adminService.GetSupportedGrainTypes();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        GrainTypes = _adminService.GetSupportedGrainTypes();

        _logger.LogInformation("SuperAdmin initiated resync for grain type: {GrainType}", SelectedType);

        if (SelectedType == "All")
        {
            Result = await _adminService.ResyncAllAsync();
        }
        else
        {
            Result = await _adminService.ResyncGrainTypeAsync(SelectedType);
        }

        _logger.LogInformation(
            "Resync completed for {GrainType}: {TotalProcessed} processed, {SuccessCount} success, {ErrorCount} errors in {Duration}",
            Result.GrainType,
            Result.TotalProcessed,
            Result.SuccessCount,
            Result.ErrorCount,
            Result.Duration);

        return Page();
    }
}

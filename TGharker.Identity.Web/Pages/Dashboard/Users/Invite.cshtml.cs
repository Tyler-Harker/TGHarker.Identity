using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Dashboard.Users;

[Authorize]
public class InviteModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly IGrainSearchService _searchService;
    private readonly ILogger<InviteModel> _logger;

    public InviteModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ILogger<InviteModel> logger)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<TenantRole> AvailableRoles { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = WellKnownRoles.User;
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task OnGetAsync()
    {
        ViewData["ActivePage"] = "Users";
        await LoadRolesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["ActivePage"] = "Users";

        if (!ModelState.IsValid)
        {
            await LoadRolesAsync();
            return Page();
        }

        var tenantId = GetTenantId();
        var email = Input.Email.ToLowerInvariant().Trim();

        // Check if user exists using search
        var userGrain = await _searchService.GetUserByEmailAsync(email);
        var userState = userGrain != null ? await userGrain.GetStateAsync() : null;

        if (userState == null)
        {
            ModelState.AddModelError("Input.Email", "No user found with this email. They must register first.");
            await LoadRolesAsync();
            return Page();
        }

        var userId = userState.Id;

        // Check if already a member
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var memberIds = await tenantGrain.GetMemberUserIdsAsync();

        if (memberIds.Contains(userId))
        {
            ModelState.AddModelError("Input.Email", "This user is already a member of this tenant.");
            await LoadRolesAsync();
            return Page();
        }

        // Create membership
        var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
        var result = await membershipGrain.CreateAsync(new CreateMembershipRequest
        {
            UserId = userId,
            TenantId = tenantId,
            Roles = [Input.Role]
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "Failed to add user to tenant.";
            await LoadRolesAsync();
            return Page();
        }

        // Add to tenant's member list
        await tenantGrain.AddMemberAsync(userId);

        // Add tenant to user's memberships
        await userGrain!.AddTenantMembershipAsync(tenantId);

        _logger.LogInformation("User {UserId} added to tenant {TenantId} with role {Role}",
            userId, tenantId, Input.Role);

        TempData["SuccessMessage"] = $"User {email} has been added to the tenant.";
        return RedirectToPage("./Index");
    }

    private async Task LoadRolesAsync()
    {
        var tenantId = GetTenantId();
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var roles = await tenantGrain.GetRolesAsync();
        AvailableRoles = roles.ToList();
    }
}

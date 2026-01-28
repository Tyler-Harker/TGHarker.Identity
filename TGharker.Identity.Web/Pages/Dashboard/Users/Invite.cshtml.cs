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
    public string? InviteUrl { get; set; }
    public bool ShowInviteLink { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = WellKnownRoles.User;
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    private string GetUserId() => User.FindFirst("user_id")?.Value
        ?? throw new InvalidOperationException("No user in session");

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
        var currentUserId = GetUserId();
        var email = Input.Email.ToLowerInvariant().Trim();

        // Check if user exists using search
        var userGrain = await _searchService.GetUserByEmailAsync(email);
        var userState = userGrain != null ? await userGrain.GetStateAsync() : null;

        if (userState == null)
        {
            // User doesn't exist - create invitation
            return await CreateInvitationAsync(tenantId, email, currentUserId);
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

        // Create membership directly for existing user
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

    private async Task<IActionResult> CreateInvitationAsync(string tenantId, string email, string invitedByUserId)
    {
        // Check if there's already a pending invitation
        var existingInvitation = await _searchService.GetPendingInvitationAsync(tenantId, email);
        if (existingInvitation != null)
        {
            var existingState = await existingInvitation.GetStateAsync();
            if (existingState != null)
            {
                // Show existing invitation link
                InviteUrl = GenerateInviteUrl(existingState.Token);
                ShowInviteLink = true;
                await LoadRolesAsync();

                _logger.LogInformation("Returning existing invitation for {Email} to tenant {TenantId}",
                    email, tenantId);

                return Page();
            }
        }

        // Create new invitation
        var invitationId = Guid.CreateVersion7().ToString();
        var invitationGrain = _clusterClient.GetGrain<IInvitationGrain>($"{tenantId}/invitation-{invitationId}");

        var result = await invitationGrain.CreateAsync(new CreateInvitationRequest
        {
            TenantId = tenantId,
            Email = email,
            InvitedByUserId = invitedByUserId,
            Roles = [Input.Role]
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "Failed to create invitation.";
            await LoadRolesAsync();
            return Page();
        }

        InviteUrl = GenerateInviteUrl(result.Token!);
        ShowInviteLink = true;
        await LoadRolesAsync();

        _logger.LogInformation("Created invitation for {Email} to tenant {TenantId} with role {Role}",
            email, tenantId, Input.Role);

        return Page();
    }

    private string GenerateInviteUrl(string token)
    {
        var request = HttpContext.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}";
        return $"{baseUrl}/Account/AcceptInvite?token={Uri.EscapeDataString(token)}";
    }

    private async Task LoadRolesAsync()
    {
        var tenantId = GetTenantId();
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        var roles = await tenantGrain.GetRolesAsync();
        AvailableRoles = roles.ToList();
    }
}

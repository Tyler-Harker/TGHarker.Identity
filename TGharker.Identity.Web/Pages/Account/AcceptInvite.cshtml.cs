using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Account;

public class AcceptInviteModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly IGrainSearchService _searchService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<AcceptInviteModel> _logger;

    public AcceptInviteModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ISessionService sessionService,
        ILogger<AcceptInviteModel> logger)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
        _sessionService = sessionService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public string? ErrorMessage { get; set; }
    public InvitationState? Invitation { get; set; }
    public TenantState? Tenant { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(Token))
        {
            ErrorMessage = "Invalid invitation link.";
            return Page();
        }

        // Find invitation by token
        var invitationGrain = await _searchService.GetInvitationByTokenAsync(Token);
        if (invitationGrain == null)
        {
            ErrorMessage = "This invitation is invalid or has expired.";
            return Page();
        }

        Invitation = await invitationGrain.GetStateAsync();
        if (Invitation == null || !await invitationGrain.IsValidAsync())
        {
            ErrorMessage = "This invitation is no longer valid.";
            return Page();
        }

        // Get tenant info
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(Invitation.TenantId);
        Tenant = await tenantGrain.GetStateAsync();

        if (Tenant == null)
        {
            ErrorMessage = "The organization for this invitation no longer exists.";
            return Page();
        }

        // Check if user is logged in
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirst("user_id")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                // Accept invitation for logged-in user
                return await AcceptInvitationAsync(invitationGrain, userId);
            }
        }

        // User not logged in - show options to register or login
        return Page();
    }

    public async Task<IActionResult> OnPostAcceptAsync()
    {
        if (string.IsNullOrEmpty(Token))
        {
            ErrorMessage = "Invalid invitation link.";
            return Page();
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Account/AcceptInvite?token={Uri.EscapeDataString(Token)}" });
        }

        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToPage("/Account/Login", new { returnUrl = $"/Account/AcceptInvite?token={Uri.EscapeDataString(Token)}" });
        }

        var invitationGrain = await _searchService.GetInvitationByTokenAsync(Token);
        if (invitationGrain == null)
        {
            ErrorMessage = "This invitation is invalid or has expired.";
            return Page();
        }

        return await AcceptInvitationAsync(invitationGrain, userId);
    }

    private async Task<IActionResult> AcceptInvitationAsync(IInvitationGrain invitationGrain, string userId)
    {
        var result = await invitationGrain.AcceptAsync(userId);

        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "Failed to accept invitation.";
            return Page();
        }

        _logger.LogInformation("User {UserId} accepted invitation and joined tenant {TenantId}",
            userId, result.TenantId);

        // Get user info for session update
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var userState = await userGrain.GetStateAsync();

        if (userState != null && !string.IsNullOrEmpty(result.TenantId))
        {
            // Update session to include new tenant (switch to it)
            await _sessionService.CompleteSessionAsync(HttpContext, userId, userState, result.TenantId, false);
        }

        TempData["SuccessMessage"] = "You have successfully joined the organization.";
        return RedirectToPage("/Dashboard/Index");
    }
}

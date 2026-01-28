using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Account;

public class AcceptOrgInviteModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly IGrainSearchService _searchService;
    private readonly ILogger<AcceptOrgInviteModel> _logger;

    public AcceptOrgInviteModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ILogger<AcceptOrgInviteModel> logger)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public OrganizationInvitationState? Invitation { get; set; }
    public OrganizationState? Organization { get; set; }
    public TenantState? Tenant { get; set; }
    public string? ErrorMessage { get; set; }
    public bool InvitationAccepted { get; set; }

    private string? GetUserId()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return null;

        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(Token))
        {
            ErrorMessage = "No invitation token provided.";
            return Page();
        }

        // Load invitation
        var invitationGrain = await _searchService.GetOrganizationInvitationByTokenAsync(Token);
        if (invitationGrain == null)
        {
            ErrorMessage = "Invitation not found or has expired.";
            return Page();
        }

        Invitation = await invitationGrain.GetStateAsync();
        if (Invitation == null || !await invitationGrain.IsValidAsync())
        {
            ErrorMessage = "This invitation is no longer valid.";
            return Page();
        }

        // Load organization and tenant info
        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{Invitation.TenantId}/org-{Invitation.OrganizationId}");
        Organization = await orgGrain.GetStateAsync();

        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(Invitation.TenantId);
        Tenant = await tenantGrain.GetStateAsync();

        // If user is logged in, check if they can accept
        var userId = GetUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            // Check if email matches
            var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
            var user = await userGrain.GetStateAsync();

            if (user != null && !user.Email.Equals(Invitation.Email, StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = $"This invitation was sent to {Invitation.Email}, but you're logged in as {user.Email}. Please sign out and use the correct account.";
                return Page();
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(Token))
        {
            ErrorMessage = "No invitation token provided.";
            return Page();
        }

        // Check if user is logged in
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            // Redirect to register with org_invite token
            return RedirectToPage("/Account/Register", new { org_invite = Token });
        }

        // Load invitation
        var invitationGrain = await _searchService.GetOrganizationInvitationByTokenAsync(Token);
        if (invitationGrain == null)
        {
            ErrorMessage = "Invitation not found or has expired.";
            return Page();
        }

        Invitation = await invitationGrain.GetStateAsync();
        if (Invitation == null || !await invitationGrain.IsValidAsync())
        {
            ErrorMessage = "This invitation is no longer valid.";
            return Page();
        }

        // Load organization and tenant info for display
        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{Invitation.TenantId}/org-{Invitation.OrganizationId}");
        Organization = await orgGrain.GetStateAsync();

        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(Invitation.TenantId);
        Tenant = await tenantGrain.GetStateAsync();

        // Check if email matches logged in user
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();

        if (user != null && !user.Email.Equals(Invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = $"This invitation was sent to {Invitation.Email}, but you're logged in as {user.Email}. Please sign out and use the correct account.";
            return Page();
        }

        // Accept the invitation
        var result = await invitationGrain.AcceptAsync(userId);

        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "Failed to accept invitation.";
            return Page();
        }

        _logger.LogInformation("User {UserId} accepted organization invitation to org {OrgId} in tenant {TenantId}",
            userId, result.OrganizationId, result.TenantId);

        InvitationAccepted = true;
        return Page();
    }
}

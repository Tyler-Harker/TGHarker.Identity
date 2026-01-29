using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Core.Extensions;

namespace TGharker.Identity.Web.Pages.Tenant;

public class ForgotPasswordModel : TenantAuthPageModel
{
    public ForgotPasswordModel(
        IClusterClient clusterClient,
        ILogger<ForgotPasswordModel> logger)
        : base(clusterClient, logger)
    {
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool EmailSent { get; set; }

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Look up user by email
        var normalizedEmail = Input.Email.ToLowerInvariant();
        var emailLock = ClusterClient.GetGrain<IUserEmailLockGrain>(normalizedEmail);
        var userId = await emailLock.GetOwnerAsync();

        if (!string.IsNullOrEmpty(userId))
        {
            // Verify user is a member of this tenant
            var userGrain = ClusterClient.GetGrain<IUserGrain>($"user-{userId}");
            var memberships = await userGrain.GetTenantMembershipsAsync();

            if (memberships.Contains(Tenant!.Id))
            {
                // Generate password reset token
                var resetToken = await userGrain.GeneratePasswordResetTokenAsync();

                // TODO: Send email with reset link
                // The reset link would be: /Tenant/{TenantId}/ResetPassword?token={resetToken}&email={email}
                Logger.LogInformation(
                    "Password reset requested for user {UserId} in tenant {TenantId}. Token: {Token}",
                    userId, Tenant.Id, resetToken);

                // In production, you would send an email here with the reset link
                // For now, we just log it and show success
            }
        }

        // Always show success to prevent email enumeration
        EmailSent = true;
        return Page();
    }
}

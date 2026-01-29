using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Tenant;

public class RegisterModel : TenantAuthPageModel
{
    private readonly IGrainSearchService _searchService;

    public RegisterModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ILogger<RegisterModel> logger)
        : base(clusterClient, logger)
    {
        _searchService = searchService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true, Name = "invite")]
    public string? InviteToken { get; set; }

    public string? ErrorMessage { get; set; }

    // Invitation info for display
    public InvitationState? Invitation { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "First Name")]
        public string? GivenName { get; set; }

        [Display(Name = "Last Name")]
        public string? FamilyName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "Password must be at least {2} characters long.", MinimumLength = 12)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        await LoadInvitationAsync();

        return Page();
    }

    private async Task LoadInvitationAsync()
    {
        if (string.IsNullOrEmpty(InviteToken))
            return;

        var invitationGrain = await _searchService.GetInvitationByTokenAsync(InviteToken);
        if (invitationGrain == null)
            return;

        Invitation = await invitationGrain.GetStateAsync();
        if (Invitation == null || !await invitationGrain.IsValidAsync())
        {
            Invitation = null;
            return;
        }

        // Verify invitation is for this tenant
        if (Invitation.TenantId != Tenant?.Id)
        {
            Invitation = null;
            return;
        }

        // Pre-fill email from invitation
        Input.Email = Invitation.Email;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        await LoadInvitationAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Validate password complexity
        if (!ValidatePasswordComplexity(Input.Password))
        {
            ErrorMessage = "Password must contain uppercase, lowercase, number, and special character.";
            return Page();
        }

        // Check if email already exists
        if (await _searchService.EmailExistsAsync(Input.Email))
        {
            ErrorMessage = "An account with this email already exists.";
            return Page();
        }

        // Create user
        var userId = Guid.CreateVersion7().ToString();
        var userGrain = ClusterClient.GetGrain<IUserGrain>($"user-{userId}");

        var passwordHash = HashPassword(Input.Password);

        var result = await userGrain.CreateAsync(new CreateUserRequest
        {
            Email = Input.Email.ToLowerInvariant(),
            Password = passwordHash,
            GivenName = Input.GivenName,
            FamilyName = Input.FamilyName
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "Failed to create account.";
            return Page();
        }

        Logger.LogInformation("Created new user {UserId} with email {Email}", userId, Input.Email);

        // Handle tenant invitation if present
        if (!string.IsNullOrEmpty(InviteToken) && Invitation != null)
        {
            var invitationGrain = await _searchService.GetInvitationByTokenAsync(InviteToken);
            if (invitationGrain != null && await invitationGrain.IsValidAsync())
            {
                var acceptResult = await invitationGrain.AcceptAsync(userId);
                if (acceptResult.Success)
                {
                    Logger.LogInformation("User {UserId} registered and accepted invitation to tenant {TenantId}",
                        userId, acceptResult.TenantId);

                    // Redirect to tenant login with success message
                    return RedirectToPage("/Tenant/Login", new
                    {
                        tenantId = TenantId,
                        returnUrl = ReturnUrl,
                        registered = true
                    });
                }
                else
                {
                    Logger.LogWarning("User {UserId} registered but failed to accept invitation: {Error}",
                        userId, acceptResult.Error);
                }
            }
        }
        else
        {
            // No invitation - add user to this tenant directly
            var membershipGrain = ClusterClient.GetGrain<ITenantMembershipGrain>($"{Tenant!.Id}/member-{userId}");
            await membershipGrain.CreateAsync(new CreateMembershipRequest
            {
                UserId = userId,
                TenantId = Tenant.Id,
                Roles = [WellKnownRoles.User]
            });

            // Add tenant to user's memberships
            await userGrain.AddTenantMembershipAsync(Tenant.Id);

            // Add user to tenant's member list
            var tenantGrain = ClusterClient.GetGrain<ITenantGrain>(Tenant.Id);
            await tenantGrain.AddMemberAsync(userId);

            Logger.LogInformation("Added user {UserId} to tenant {TenantId}", userId, Tenant.Id);
        }

        // Redirect to tenant login
        return RedirectToPage("/Tenant/Login", new
        {
            tenantId = TenantId,
            returnUrl = ReturnUrl,
            registered = true
        });
    }

    private static bool ValidatePasswordComplexity(string password)
    {
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }

    private static string HashPassword(string password)
    {
        const int iterations = 100000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}

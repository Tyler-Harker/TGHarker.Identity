using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Pages.Tenant;

public class ResetPasswordModel : TenantAuthPageModel
{
    public ResetPasswordModel(
        IClusterClient clusterClient,
        ILogger<ResetPasswordModel> logger)
        : base(clusterClient, logger)
    {
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    public bool PasswordReset { get; set; }

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "Password must be at least {2} characters long.", MinimumLength = 12)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(Email))
        {
            ErrorMessage = "Invalid password reset link.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(Email))
        {
            ErrorMessage = "Invalid password reset link.";
            return Page();
        }

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

        // Look up user by email
        var normalizedEmail = Email.ToLowerInvariant();
        var emailLock = ClusterClient.GetGrain<IUserEmailLockGrain>(normalizedEmail);
        var userId = await emailLock.GetOwnerAsync();

        if (string.IsNullOrEmpty(userId))
        {
            ErrorMessage = "Invalid password reset link.";
            return Page();
        }

        var userGrain = ClusterClient.GetGrain<IUserGrain>($"user-{userId}");

        // Verify user is a member of this tenant
        var memberships = await userGrain.GetTenantMembershipsAsync();
        if (!memberships.Contains(Tenant!.Id))
        {
            ErrorMessage = "Invalid password reset link.";
            return Page();
        }

        // Reset the password
        var newPasswordHash = HashPassword(Input.Password);
        var success = await userGrain.ResetPasswordAsync(Token, newPasswordHash);

        if (!success)
        {
            ErrorMessage = "This reset link has expired or has already been used.";
            return Page();
        }

        Logger.LogInformation("User {UserId} reset their password via tenant {TenantId}", userId, Tenant.Id);

        PasswordReset = true;
        return Page();
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

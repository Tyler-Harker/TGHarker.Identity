using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TGHarker.Identity.Abstractions.Grains;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Tenant;

public class LoginModel : TenantAuthPageModel
{
    private readonly ISessionService _sessionService;

    public LoginModel(
        IClusterClient clusterClient,
        ISessionService sessionService,
        ILogger<LoginModel> logger)
        : base(clusterClient, logger)
    {
        _sessionService = sessionService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await ResolveTenantAsync())
        {
            return TenantNotFound();
        }

        // Clear existing authentication
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

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

        // Look up user by email using the email lock grain
        var normalizedEmail = Input.Email.ToLowerInvariant();
        var emailLock = ClusterClient.GetGrain<IUserEmailLockGrain>(normalizedEmail);
        var userId = await emailLock.GetOwnerAsync();

        if (string.IsNullOrEmpty(userId))
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        // Get user grain and state
        var userGrain = ClusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();

        if (user == null)
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        // Check if locked out
        if (await userGrain.IsLockedOutAsync())
        {
            ErrorMessage = "Account is locked. Please try again later.";
            return Page();
        }

        // Verify password
        if (!VerifyPassword(Input.Password, user.PasswordHash))
        {
            await userGrain.RecordLoginAttemptAsync(false, HttpContext.Connection.RemoteIpAddress?.ToString());
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        // Record successful login
        await userGrain.RecordLoginAttemptAsync(true, HttpContext.Connection.RemoteIpAddress?.ToString());

        // Verify user is a member of this tenant
        var memberships = await userGrain.GetTenantMembershipsAsync();

        if (!memberships.Contains(Tenant!.Id))
        {
            ErrorMessage = "You are not a member of this organization.";
            return Page();
        }

        // Create complete session with this tenant
        await _sessionService.CompleteSessionAsync(HttpContext, userId, user, Tenant.Id, Input.RememberMe);

        Logger.LogInformation("User {UserId} logged in to tenant {TenantId} via tenant-specific login", userId, Tenant.Id);

        return Redirect(GetEffectiveReturnUrl());
    }

    private static bool VerifyPassword(string password, string hashedPassword)
    {
        var parts = hashedPassword.Split('.');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], out var iterations))
            return false;

        byte[] salt;
        byte[] storedHash;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            storedHash = Convert.FromBase64String(parts[2]);
        }
        catch
        {
            return false;
        }

        var computedHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }
}

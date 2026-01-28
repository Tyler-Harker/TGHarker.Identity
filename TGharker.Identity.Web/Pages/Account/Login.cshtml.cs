using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ISessionService _sessionService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        IClusterClient clusterClient,
        ISessionService sessionService,
        ILogger<LoginModel> logger)
    {
        _clusterClient = clusterClient;
        _sessionService = sessionService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true, Name = "tenant")]
    public string? TenantIdentifier { get; set; }

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Clear existing authentication
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Look up user by email using the email lock grain
        var normalizedEmail = Input.Email.ToLowerInvariant();
        var emailLock = _clusterClient.GetGrain<IUserEmailLockGrain>(normalizedEmail);
        var userId = await emailLock.GetOwnerAsync();

        if (string.IsNullOrEmpty(userId))
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        // Get user grain and state
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
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

        // Get user's tenant memberships
        var memberships = await userGrain.GetTenantMembershipsAsync();

        string? selectedTenantId = null;

        // If tenant specified in request, verify membership
        if (!string.IsNullOrEmpty(TenantIdentifier))
        {
            var normalizedTenantId = TenantIdentifier.ToLowerInvariant();
            var tenantGrain = await _clusterClient.Search<ITenantGrain>()
                .Where(t => t.Identifier == normalizedTenantId && t.IsActive)
                .FirstOrDefaultAsync();
            var tenantState = tenantGrain != null ? await tenantGrain.GetStateAsync() : null;

            if (tenantState != null && memberships.Contains(tenantState.Id))
            {
                selectedTenantId = tenantState.Id;
            }
            else
            {
                ErrorMessage = "You are not a member of this organization.";
                return Page();
            }
        }
        else if (memberships.Count == 1)
        {
            // Auto-select single tenant
            selectedTenantId = memberships[0];
        }
        else
        {
            // Zero or multiple tenants - create partial session and redirect to tenant landing page
            await _sessionService.CreatePartialSessionAsync(HttpContext, userId, user, Input.RememberMe);

            _logger.LogInformation("User {UserId} logged in with partial session (requires tenant selection)", userId);

            return RedirectToPage("/Tenants/Index", new { returnUrl = ReturnUrl });
        }

        // Single tenant or specific tenant requested - complete session
        await _sessionService.CompleteSessionAsync(HttpContext, userId, user, selectedTenantId!, Input.RememberMe);

        _logger.LogInformation("User {UserId} logged in to tenant {TenantId}", userId, selectedTenantId);

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
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

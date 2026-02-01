using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Tenant;

public class LoginModel : TenantAuthPageModel
{
    private readonly ISessionService _sessionService;
    private readonly IUserFlowService _userFlowService;

    public LoginModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ISessionService sessionService,
        IUserFlowService userFlowService,
        ILogger<LoginModel> logger)
        : base(clusterClient, searchService, logger)
    {
        _sessionService = sessionService;
        _userFlowService = userFlowService;
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

        // Check if user needs to set up an organization based on client's UserFlow settings
        var userFlow = await _userFlowService.ResolveUserFlowFromReturnUrlAsync(Tenant.Id, ReturnUrl);

        Logger.LogInformation(
            "User {UserId} login - TenantId={TenantId}, UserFlow resolved: {UserFlowResolved}, OrganizationsEnabled={OrganizationsEnabled}, Mode={Mode}",
            userId, Tenant.Id, userFlow != null, userFlow?.OrganizationsEnabled, userFlow?.OrganizationMode);
        Logger.LogDebug("ReturnUrl for UserFlow resolution: {ReturnUrl}", ReturnUrl);

        if (userFlow?.OrganizationsEnabled == true &&
            userFlow.OrganizationMode != OrganizationRegistrationMode.None)
        {
            // Check if user already has an organization in this tenant
            var userOrgs = user.OrganizationMemberships
                .Where(m => m.TenantId == Tenant.Id)
                .ToList();

            if (userOrgs.Count == 0)
            {
                // User needs to set up an organization - create session first so they're authenticated
                await _sessionService.CompleteSessionAsync(HttpContext, userId, user, Tenant.Id, Input.RememberMe);

                Logger.LogInformation("User {UserId} logged in but needs organization setup", userId);

                // Redirect to organization setup page with OAuth parameters preserved individually
                var setupUrl = BuildSetupOrganizationUrl();
                return Redirect(setupUrl);
            }
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

    private string BuildSetupOrganizationUrl()
    {
        var baseUrl = $"/tenant/{TenantId}/setup-organization";

        // If ReturnUrl is not an authorize URL, just pass it through
        if (string.IsNullOrEmpty(ReturnUrl) || !ReturnUrl.Contains("/connect/authorize"))
        {
            return $"{baseUrl}?returnUrl={HttpUtility.UrlEncode(ReturnUrl)}";
        }

        // Parse OAuth parameters from ReturnUrl to avoid double-encoding issues
        try
        {
            var decodedUrl = HttpUtility.UrlDecode(ReturnUrl);
            var queryIndex = decodedUrl.IndexOf('?');
            if (queryIndex < 0)
            {
                return $"{baseUrl}?returnUrl={HttpUtility.UrlEncode(ReturnUrl)}";
            }

            var queryString = decodedUrl[(queryIndex + 1)..];
            var queryParams = HttpUtility.ParseQueryString(queryString);

            // Use Uri.EscapeDataString instead of HttpUtility.UrlEncode to properly encode
            // special characters like '+' as '%2B' (UrlEncode treats + as space)
            var setupUrl = $"{baseUrl}?" +
                $"client_id={Uri.EscapeDataString(queryParams["client_id"] ?? "")}" +
                $"&scope={Uri.EscapeDataString(queryParams["scope"] ?? "")}" +
                $"&redirect_uri={Uri.EscapeDataString(queryParams["redirect_uri"] ?? "")}" +
                $"&state={Uri.EscapeDataString(queryParams["state"] ?? "")}" +
                $"&nonce={Uri.EscapeDataString(queryParams["nonce"] ?? "")}" +
                $"&code_challenge={Uri.EscapeDataString(queryParams["code_challenge"] ?? "")}" +
                $"&code_challenge_method={Uri.EscapeDataString(queryParams["code_challenge_method"] ?? "")}" +
                $"&response_mode={Uri.EscapeDataString(queryParams["response_mode"] ?? "")}";

            Logger.LogDebug("Built setup organization URL with individual OAuth params, state={State}", queryParams["state"]);

            return setupUrl;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse OAuth params from ReturnUrl, using fallback");
            return $"{baseUrl}?returnUrl={HttpUtility.UrlEncode(ReturnUrl)}";
        }
    }
}

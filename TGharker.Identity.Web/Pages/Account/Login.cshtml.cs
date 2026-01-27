using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(IClusterClient clusterClient, ILogger<LoginModel> logger)
    {
        _clusterClient = clusterClient;
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

        // Look up user by email
        var userRegistry = _clusterClient.GetGrain<IUserRegistryGrain>("user-registry");
        var userId = await userRegistry.GetUserIdByEmailAsync(Input.Email.ToLowerInvariant());

        if (string.IsNullOrEmpty(userId))
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        // Get user
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

        if (memberships.Count == 0)
        {
            ErrorMessage = "You are not a member of any organization.";
            return Page();
        }

        string? selectedTenantId = null;

        // If tenant specified in request, verify membership
        if (!string.IsNullOrEmpty(TenantIdentifier))
        {
            var tenantRegistry = _clusterClient.GetGrain<ITenantRegistryGrain>("tenant-registry");
            var tenantId = await tenantRegistry.GetTenantIdByIdentifierAsync(TenantIdentifier);

            if (!string.IsNullOrEmpty(tenantId) && memberships.Contains(tenantId))
            {
                selectedTenantId = tenantId;
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
            // Multiple tenants - redirect to tenant selection
            return RedirectToPage("/Account/SelectTenant", new
            {
                returnUrl = ReturnUrl,
                userId = userId
            });
        }

        // Get tenant info
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(selectedTenantId!);
        var tenant = await tenantGrain.GetStateAsync();

        // Get membership for roles
        var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{selectedTenantId}/member-{userId}");
        var membership = await membershipGrain.GetStateAsync();

        // Create claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new(ClaimTypes.Email, user.Email),
            new("tenant_id", selectedTenantId!),
            new("tenant_identifier", tenant?.Identifier ?? "")
        };

        if (!string.IsNullOrEmpty(user.GivenName))
            claims.Add(new Claim(ClaimTypes.GivenName, user.GivenName));

        if (!string.IsNullOrEmpty(user.FamilyName))
            claims.Add(new Claim(ClaimTypes.Surname, user.FamilyName));

        // Add roles
        if (membership != null)
        {
            foreach (var role in membership.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = Input.RememberMe,
            ExpiresUtc = Input.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

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

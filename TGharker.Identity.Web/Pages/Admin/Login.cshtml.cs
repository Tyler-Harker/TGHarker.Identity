using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TGharker.Identity.Web.Pages.Admin;

public class LoginModel : PageModel
{
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(ILogger<LoginModel> logger)
    {
        _logger = logger;
    }

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var expectedUsername = Environment.GetEnvironmentVariable("SUPERADMIN_USERNAME");
        var expectedPassword = Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD");

        if (string.IsNullOrEmpty(expectedUsername) || string.IsNullOrEmpty(expectedPassword))
        {
            _logger.LogWarning("SuperAdmin credentials not configured");
            ErrorMessage = "SuperAdmin access is not configured.";
            return Page();
        }

        if (Username == expectedUsername && Password == expectedPassword)
        {
            var claims = new[]
            {
                new Claim("is_superadmin", "true"),
                new Claim(ClaimTypes.Name, "SuperAdmin"),
                new Claim(ClaimTypes.Role, "SuperAdmin")
            };

            var identity = new ClaimsIdentity(claims, "SuperAdmin");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("SuperAdmin", principal, new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            });

            _logger.LogInformation("SuperAdmin login successful");
            return RedirectToPage("/Admin/Index");
        }

        _logger.LogWarning("Failed SuperAdmin login attempt for username: {Username}", Username);
        ErrorMessage = "Invalid credentials.";
        return Page();
    }
}

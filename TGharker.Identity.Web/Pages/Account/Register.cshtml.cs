using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly IGrainSearchService _searchService;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        IClusterClient clusterClient,
        IGrainSearchService searchService,
        ILogger<RegisterModel> logger)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true, Name = "tenant")]
    public string? TenantIdentifier { get; set; }

    [BindProperty(SupportsGet = true, Name = "invite")]
    public string? InviteToken { get; set; }

    [BindProperty(SupportsGet = true, Name = "org_invite")]
    public string? OrgInviteToken { get; set; }

    public string? ErrorMessage { get; set; }

    // Invitation info for display
    public InvitationState? Invitation { get; set; }
    public TenantState? InvitationTenant { get; set; }

    // Organization invitation info
    public OrganizationInvitationState? OrgInvitation { get; set; }
    public OrganizationState? InvitationOrganization { get; set; }
    public TenantState? OrgInvitationTenant { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "First Name")]
        public string? GivenName { get; set; }

        [Display(Name = "Last Name")]
        public string? FamilyName { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 12)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync()
    {
        await LoadInvitationAsync();
        await LoadOrgInvitationAsync();
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

        // Pre-fill email from invitation
        Input.Email = Invitation.Email;

        // Load tenant info
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(Invitation.TenantId);
        InvitationTenant = await tenantGrain.GetStateAsync();
    }

    private async Task LoadOrgInvitationAsync()
    {
        if (string.IsNullOrEmpty(OrgInviteToken))
            return;

        var invitationGrain = await _searchService.GetOrganizationInvitationByTokenAsync(OrgInviteToken);
        if (invitationGrain == null)
            return;

        OrgInvitation = await invitationGrain.GetStateAsync();
        if (OrgInvitation == null || !await invitationGrain.IsValidAsync())
        {
            OrgInvitation = null;
            return;
        }

        // Pre-fill email from invitation
        Input.Email = OrgInvitation.Email;

        // Load tenant and organization info
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(OrgInvitation.TenantId);
        OrgInvitationTenant = await tenantGrain.GetStateAsync();

        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{OrgInvitation.TenantId}/org-{OrgInvitation.OrganizationId}");
        InvitationOrganization = await orgGrain.GetStateAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Load invitations if present (for validation)
        await LoadInvitationAsync();
        await LoadOrgInvitationAsync();

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

        // Check if email already exists using search
        if (await _searchService.EmailExistsAsync(Input.Email))
        {
            ErrorMessage = "An account with this email already exists.";
            return Page();
        }

        // Create user
        var userId = Guid.CreateVersion7().ToString();
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");

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

        _logger.LogInformation("Created new user {UserId} with email {Email}", userId, Input.Email);

        // Handle tenant invitation if present
        if (!string.IsNullOrEmpty(InviteToken))
        {
            var invitationGrain = await _searchService.GetInvitationByTokenAsync(InviteToken);
            if (invitationGrain != null && await invitationGrain.IsValidAsync())
            {
                var acceptResult = await invitationGrain.AcceptAsync(userId);
                if (acceptResult.Success)
                {
                    _logger.LogInformation("User {UserId} registered and accepted invitation to tenant {TenantId}",
                        userId, acceptResult.TenantId);

                    // Redirect to login with success message
                    return RedirectToPage("/Account/Login", new
                    {
                        returnUrl = ReturnUrl,
                        registered = true,
                        joined = true
                    });
                }
                else
                {
                    _logger.LogWarning("User {UserId} registered but failed to accept invitation: {Error}",
                        userId, acceptResult.Error);
                }
            }
        }
        // Handle organization invitation if present
        else if (!string.IsNullOrEmpty(OrgInviteToken))
        {
            var orgInvitationGrain = await _searchService.GetOrganizationInvitationByTokenAsync(OrgInviteToken);
            if (orgInvitationGrain != null && await orgInvitationGrain.IsValidAsync())
            {
                var acceptResult = await orgInvitationGrain.AcceptAsync(userId);
                if (acceptResult.Success)
                {
                    _logger.LogInformation("User {UserId} registered and accepted organization invitation to org {OrgId} in tenant {TenantId}",
                        userId, acceptResult.OrganizationId, acceptResult.TenantId);

                    // Redirect to login with success message
                    return RedirectToPage("/Account/Login", new
                    {
                        returnUrl = ReturnUrl,
                        registered = true,
                        joined_org = true
                    });
                }
                else
                {
                    _logger.LogWarning("User {UserId} registered but failed to accept organization invitation: {Error}",
                        userId, acceptResult.Error);
                }
            }
        }
        // If tenant is specified (but no invitation), add user to tenant
        else if (!string.IsNullOrEmpty(TenantIdentifier))
        {
            var tenantGrain = await _searchService.GetTenantByIdentifierAsync(TenantIdentifier);
            var tenantState = tenantGrain != null ? await tenantGrain.GetStateAsync() : null;

            if (tenantState != null)
            {
                var tenantId = tenantState.Id;

                // Create membership with user role
                var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
                await membershipGrain.CreateAsync(new CreateMembershipRequest
                {
                    UserId = userId,
                    TenantId = tenantId,
                    Roles = [WellKnownRoles.User]
                });

                // Add tenant to user's memberships
                await userGrain.AddTenantMembershipAsync(tenantId);

                // Add user to tenant's member list
                await tenantGrain!.AddMemberAsync(userId);

                _logger.LogInformation("Added user {UserId} to tenant {TenantId}", userId, tenantId);
            }
        }

        // Redirect to login
        return RedirectToPage("/Account/Login", new
        {
            returnUrl = ReturnUrl,
            tenant = TenantIdentifier,
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

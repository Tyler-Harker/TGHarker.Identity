using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients;

[Authorize]
public class EditModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<EditModel> _logger;

    public EditModel(IClusterClient clusterClient, ILogger<EditModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ClientId { get; set; } = string.Empty;
    public bool IsConfidential { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Application name is required")]
        [StringLength(100, MinimumLength = 2)]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public string RedirectUris { get; set; } = string.Empty;

        public List<string> AllowedScopes { get; set; } = [];

        public bool RequirePkce { get; set; } = true;
        public bool RequireConsent { get; set; } = true;

        public string CorsOrigins { get; set; } = string.Empty;

        // UserFlow Settings
        public bool OrganizationsEnabled { get; set; }
        public OrganizationRegistrationMode OrganizationMode { get; set; }
        public string? DefaultOrganizationRole { get; set; }
        public bool RequireOrganizationName { get; set; } = true;

        [StringLength(50)]
        public string? OrganizationNameLabel { get; set; }

        [StringLength(100)]
        public string? OrganizationNamePlaceholder { get; set; }

        [StringLength(200)]
        public string? OrganizationHelpText { get; set; }
    }

    public List<string> AvailableScopes { get; } = ["openid", "profile", "email", "phone", "address", "offline_access"];

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public async Task<IActionResult> OnGetAsync(string id)
    {
        ViewData["ActivePage"] = "Clients";

        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("./Index");
        }

        var tenantId = GetTenantId();
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{id}");
        var client = await clientGrain.GetStateAsync();

        if (client == null || !client.IsActive)
        {
            return RedirectToPage("./Index");
        }

        ClientId = id;
        IsConfidential = client.IsConfidential;

        var userFlow = client.UserFlow ?? new UserFlowSettings();

        Input = new InputModel
        {
            ClientName = client.ClientName ?? string.Empty,
            Description = client.Description,
            RedirectUris = string.Join("\n", client.RedirectUris),
            AllowedScopes = client.AllowedScopes.ToList(),
            RequirePkce = client.RequirePkce,
            RequireConsent = client.RequireConsent,
            CorsOrigins = string.Join("\n", client.CorsOrigins),
            // UserFlow settings
            OrganizationsEnabled = userFlow.OrganizationsEnabled,
            OrganizationMode = userFlow.OrganizationMode,
            DefaultOrganizationRole = userFlow.DefaultOrganizationRole,
            RequireOrganizationName = userFlow.RequireOrganizationName,
            OrganizationNameLabel = userFlow.OrganizationNameLabel,
            OrganizationNamePlaceholder = userFlow.OrganizationNamePlaceholder,
            OrganizationHelpText = userFlow.OrganizationHelpText
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        ViewData["ActivePage"] = "Clients";
        ClientId = id;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var tenantId = GetTenantId();
            var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{id}");
            var client = await clientGrain.GetStateAsync();

            if (client == null)
            {
                TempData["ErrorMessage"] = "Application not found.";
                return RedirectToPage("./Index");
            }

            IsConfidential = client.IsConfidential;

            // Parse redirect URIs (one per line)
            var redirectUris = Input.RedirectUris
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrEmpty(u))
                .ToList();

            // Parse CORS origins (one per line)
            var corsOrigins = Input.CorsOrigins
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim())
                .Where(o => !string.IsNullOrEmpty(o))
                .ToList();

            // Build UserFlow settings
            var userFlow = new UserFlowSettings
            {
                OrganizationsEnabled = Input.OrganizationsEnabled,
                OrganizationMode = Input.OrganizationMode,
                DefaultOrganizationRole = Input.DefaultOrganizationRole,
                RequireOrganizationName = Input.RequireOrganizationName,
                OrganizationNameLabel = Input.OrganizationNameLabel?.Trim(),
                OrganizationNamePlaceholder = Input.OrganizationNamePlaceholder?.Trim(),
                OrganizationHelpText = Input.OrganizationHelpText?.Trim()
            };

            // Update client
            var updateRequest = new UpdateClientRequest
            {
                ClientName = Input.ClientName.Trim(),
                Description = Input.Description?.Trim(),
                RedirectUris = redirectUris,
                AllowedScopes = Input.AllowedScopes ?? [],
                RequirePkce = Input.RequirePkce,
                RequireConsent = Input.RequireConsent,
                CorsOrigins = corsOrigins,
                UserFlow = userFlow
            };

            await clientGrain.UpdateAsync(updateRequest);

            _logger.LogInformation("Client {ClientId} updated in tenant {TenantId}", id, tenantId);

            TempData["SuccessMessage"] = $"Application '{Input.ClientName}' updated successfully.";
            return RedirectToPage("./Details", new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update client");
            ErrorMessage = "Failed to update application. Please try again.";
            return Page();
        }
    }
}

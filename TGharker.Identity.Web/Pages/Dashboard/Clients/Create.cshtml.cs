using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGharker.Identity.Web.Pages.Dashboard.Clients;

[Authorize(Policy = WellKnownPermissions.ClientsCreate)]
public class CreateModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(IClusterClient clusterClient, ILogger<CreateModel> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? NewClientSecret { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Client ID is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Client ID must be between 2 and 50 characters")]
        [RegularExpression(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$",
            ErrorMessage = "Client ID must be lowercase, contain only letters, numbers, and hyphens")]
        public string ClientId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Application name is required")]
        [StringLength(100, MinimumLength = 2)]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public string ApplicationType { get; set; } = "spa";

        public string? RedirectUri { get; set; }
    }

    private string GetTenantId() => User.FindFirst("tenant_id")?.Value
        ?? throw new InvalidOperationException("No tenant in session");

    public void OnGet()
    {
        ViewData["ActivePage"] = "Clients";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["ActivePage"] = "Clients";

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = GetTenantId();
        var clientId = Input.ClientId.ToLowerInvariant().Trim();

        // Check if client already exists
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        if (await clientGrain.ExistsAsync())
        {
            ModelState.AddModelError("Input.ClientId", "A client with this ID already exists.");
            return Page();
        }

        // Determine client configuration based on application type
        var isConfidential = Input.ApplicationType == "server";
        var grantTypes = Input.ApplicationType switch
        {
            "spa" => new List<string> { GrantTypes.AuthorizationCode },
            "server" => new List<string> { GrantTypes.ClientCredentials, GrantTypes.AuthorizationCode, GrantTypes.RefreshToken },
            "native" => new List<string> { GrantTypes.AuthorizationCode, GrantTypes.RefreshToken },
            _ => new List<string> { GrantTypes.AuthorizationCode }
        };

        var redirectUris = new List<string>();
        if (!string.IsNullOrWhiteSpace(Input.RedirectUri))
        {
            redirectUris.Add(Input.RedirectUri.Trim());
        }

        var result = await clientGrain.CreateAsync(new CreateClientRequest
        {
            TenantId = tenantId,
            ClientId = clientId,
            ClientName = Input.ClientName.Trim(),
            Description = Input.Description?.Trim(),
            IsConfidential = isConfidential,
            RequireConsent = true,
            RequirePkce = true,
            RedirectUris = redirectUris,
            AllowedScopes = ["openid", "profile", "email"],
            AllowedGrantTypes = grantTypes,
            CorsOrigins = []
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error ?? "Failed to create application.";
            return Page();
        }

        // Add client to tenant's client list
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        await tenantGrain.AddClientAsync(clientId);

        _logger.LogInformation("Client {ClientId} created in tenant {TenantId}", clientId, tenantId);

        if (isConfidential && !string.IsNullOrEmpty(result.ClientSecret))
        {
            // Store secret in TempData to display on details page
            TempData["NewClientSecret"] = result.ClientSecret;
            TempData["SuccessMessage"] = $"Application '{Input.ClientName}' created successfully. Copy the client secret now - it won't be shown again.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Application '{Input.ClientName}' created successfully.";
        }

        return RedirectToPage("./Details", new { id = clientId });
    }
}

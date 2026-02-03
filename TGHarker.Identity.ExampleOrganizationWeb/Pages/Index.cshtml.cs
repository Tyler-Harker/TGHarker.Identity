using Microsoft.AspNetCore.Mvc.RazorPages;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.ExampleOrganizationWeb.Pages;

public class IndexModel : PageModel
{
    private readonly IClusterClient _clusterClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IndexModel> _logger;

    private const string TestTenantIdentifier = "test";
    private const string TestClientId = "testorgweb";

    public IndexModel(IClusterClient clusterClient, IConfiguration configuration, ILogger<IndexModel> logger)
    {
        _clusterClient = clusterClient;
        _configuration = configuration;
        _logger = logger;
    }

    public ClientState? Client { get; private set; }
    public UserFlowSettings? UserFlow { get; private set; }
    public string IdentityAuthority { get; private set; } = string.Empty;

    public async Task OnGetAsync()
    {
        var identityUrl = _configuration["Identity:Authority"] ?? "https://localhost:7157";
        var tenantId = _configuration["Identity:TenantId"] ?? "test";
        IdentityAuthority = $"{identityUrl}/tenant/{tenantId}";

        try
        {
            // Fetch client
            var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{TestTenantIdentifier}/{TestClientId}");
            if (await clientGrain.ExistsAsync())
            {
                Client = await clientGrain.GetStateAsync();
                UserFlow = await clientGrain.GetUserFlowSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching client data");
        }
    }
}

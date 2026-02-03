using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.ExampleOrganizationWeb.Services;

public sealed class DataSeedingService : BackgroundService
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<DataSeedingService> _logger;

    private const string TestTenantIdentifier = "test";
    private const string TestClientId = "testorgweb";
    private const string TestClientName = "TestOrgWeb";

    public DataSeedingService(IClusterClient clusterClient, ILogger<DataSeedingService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for Orleans cluster to be ready
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        try
        {
            _logger.LogInformation("Starting organization client seeding...");

            // Create client/application with organization prompt mode
            await CreateClientAsync(stoppingToken);

            _logger.LogInformation("Organization client seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during organization client seeding");
        }
    }

    private async Task CreateClientAsync(CancellationToken stoppingToken)
    {
        var clientGrainKey = $"{TestTenantIdentifier}/{TestClientId}";
        var clientGrain = _clusterClient.GetGrain<IClientGrain>(clientGrainKey);

        if (await clientGrain.ExistsAsync())
        {
            _logger.LogInformation("Client {ClientId} already exists", TestClientId);
            return;
        }

        var result = await clientGrain.CreateAsync(new CreateClientRequest
        {
            TenantId = TestTenantIdentifier,
            ClientId = TestClientId,
            ClientName = TestClientName,
            Description = "Example web application for testing organization flows with prompt mode",
            IsConfidential = false,
            RequirePkce = true,
            RequireConsent = false,
            RedirectUris =
            [
                "http://localhost:5210/callback",
                "http://localhost:5210/signin-oidc",
                "https://localhost:7210/callback",
                "https://localhost:7210/signin-oidc"
            ],
            PostLogoutRedirectUris =
            [
                "http://localhost:5210/",
                "https://localhost:7210/"
            ],
            AllowedScopes = ["openid", "profile", "email"],
            AllowedGrantTypes = ["authorization_code", "refresh_token"],
            CorsOrigins =
            [
                "http://localhost:5210",
                "https://localhost:7210"
            ],
            UserFlow = new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.Prompt,
                RequireOrganizationName = true,
                OrganizationNameLabel = "Organization Name",
                OrganizationNamePlaceholder = "Enter your organization name",
                OrganizationHelpText = "Create or join an organization to collaborate with your team."
            }
        });

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create client: {result.Error}");
        }

        // Register client with tenant
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(TestTenantIdentifier);
        await tenantGrain.AddClientAsync(TestClientId);

        _logger.LogInformation("Created client {ClientId} for tenant {TenantId} with organization prompt mode", TestClientId, TestTenantIdentifier);
    }
}

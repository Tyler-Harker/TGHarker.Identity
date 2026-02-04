using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.ExampleOrganizationWeb.Services;

/// <summary>
/// Seeds initial client data via Orleans.
/// Permission/role sync is handled separately by PermissionSyncService via the SDK.
/// </summary>
public sealed class DataSeedingService(
    IClusterClient clusterClient,
    ClientSecretStore secretStore,
    ILogger<DataSeedingService> logger) : BackgroundService
{
    private const string TestTenantIdentifier = "test";
    private const string TestClientId = "testorgweb";
    private const string TestClientName = "TestOrgWeb";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for Orleans cluster to be ready
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        try
        {
            logger.LogInformation("Starting organization client seeding...");

            // Create client/application with organization prompt mode and get the secret
            var clientSecret = await CreateClientAndGetSecretAsync(stoppingToken);

            // Notify the secret store so PermissionSyncService can proceed
            secretStore.SetSecret(clientSecret);

            logger.LogInformation("Organization client seeding completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during organization client seeding");
        }
    }

    private async Task<string> CreateClientAndGetSecretAsync(CancellationToken stoppingToken)
    {
        var clientGrainKey = $"{TestTenantIdentifier}/{TestClientId}";
        var clientGrain = clusterClient.GetGrain<IClientGrain>(clientGrainKey);

        if (await clientGrain.ExistsAsync())
        {
            logger.LogInformation("Client {ClientId} already exists, creating new secret for sync", TestClientId);

            // Client exists, but we need a fresh secret for the sync service
            var secretResult = await clientGrain.AddSecretAsync("PermissionSync secret", null);
            if (!secretResult.Success || string.IsNullOrEmpty(secretResult.PlainTextSecret))
            {
                throw new InvalidOperationException($"Failed to create client secret: {secretResult.Error}");
            }

            logger.LogInformation("Created new client secret for existing client");
            return secretResult.PlainTextSecret;
        }

        var result = await clientGrain.CreateAsync(new CreateClientRequest
        {
            TenantId = TestTenantIdentifier,
            ClientId = TestClientId,
            ClientName = TestClientName,
            Description = "Example web application for testing organization flows with prompt mode",
            IsConfidential = true,  // Confidential client for CCF support
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
                "https://localhost:7210/",
                "http://localhost:5210/signout-callback-oidc",
                "https://localhost:7210/signout-callback-oidc"
            ],
            AllowedScopes = ["openid", "profile", "email"],
            AllowedGrantTypes = ["authorization_code", "refresh_token", "client_credentials"],
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

        // Add a secret for development
        var secretResult2 = await clientGrain.AddSecretAsync("Development secret", null);
        if (!secretResult2.Success || string.IsNullOrEmpty(secretResult2.PlainTextSecret))
        {
            throw new InvalidOperationException($"Failed to create client secret: {secretResult2.Error}");
        }

        logger.LogInformation("Client secret created for new client");

        // Register client with tenant
        var tenantGrain = clusterClient.GetGrain<ITenantGrain>(TestTenantIdentifier);
        await tenantGrain.AddClientAsync(TestClientId);

        logger.LogInformation("Created confidential client {ClientId} for tenant {TenantId} with organization prompt mode", TestClientId, TestTenantIdentifier);

        return secretResult2.PlainTextSecret;
    }
}

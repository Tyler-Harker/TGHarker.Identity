using System.Security.Cryptography;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.ExampleWeb.Services;

/// <summary>
/// Seeds initial data (user, tenant, client) via Orleans.
/// Permission/role sync is handled separately by PermissionSyncService via the SDK.
/// </summary>
public sealed class DataSeedingService(
    IClusterClient clusterClient,
    ClientSecretStore secretStore,
    ILogger<DataSeedingService> logger) : BackgroundService
{
    // ⚠️ HARDCODED SECRET FOR TESTING/EXAMPLE PURPOSES ONLY - DO NOT USE IN PRODUCTION ⚠️
    private const string TestClientSecret = "Hardcoded testing secret";

    private const string TestEmail = "test@test.com";
    private const string TestPassword = "Password!23";
    private const string TestTenantIdentifier = "test";
    private const string TestTenantName = "Test";
    private const string TestClientId = "testweb";
    private const string TestClientName = "TestWeb";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for Orleans cluster to be ready
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        try
        {
            logger.LogInformation("Starting data seeding...");

            // Step 1: Create user
            var userId = await CreateUserAsync(stoppingToken);

            // Step 2: Create tenant
            await CreateTenantAsync(userId, stoppingToken);

            // Step 3: Create client/application and get the secret
            var clientSecret = await CreateClientAndGetSecretAsync(stoppingToken);

            // Notify the secret store so PermissionSyncService can proceed
            secretStore.SetSecret(clientSecret);

            logger.LogInformation("Data seeding completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during data seeding");
        }
    }

    private async Task<string> CreateUserAsync(CancellationToken stoppingToken)
    {
        var userRegistry = clusterClient.GetGrain<IUserRegistryGrain>("user-registry");
        var existingUserId = await userRegistry.GetUserIdByEmailAsync(TestEmail);

        if (!string.IsNullOrEmpty(existingUserId))
        {
            logger.LogInformation("User {Email} already exists with ID {UserId}", TestEmail, existingUserId);
            return existingUserId;
        }

        var userId = Guid.NewGuid().ToString();
        var userGrain = clusterClient.GetGrain<IUserGrain>($"user-{userId}");

        var hashedPassword = HashPassword(TestPassword);

        var result = await userGrain.CreateAsync(new CreateUserRequest
        {
            Email = TestEmail,
            Password = hashedPassword,
            GivenName = "Test",
            FamilyName = "User"
        });

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create user: {result.Error}");
        }

        // Register in user registry
        var registered = await userRegistry.RegisterUserAsync(TestEmail, userId);
        if (!registered)
        {
            throw new InvalidOperationException("Failed to register user in registry");
        }

        logger.LogInformation("Created user {Email} with ID {UserId}", TestEmail, userId);
        return userId;
    }

    private async Task CreateTenantAsync(string userId, CancellationToken stoppingToken)
    {
        var tenantRegistry = clusterClient.GetGrain<ITenantRegistryGrain>("tenant-registry");
        var existingTenantId = await tenantRegistry.GetTenantIdByIdentifierAsync(TestTenantIdentifier);

        if (!string.IsNullOrEmpty(existingTenantId))
        {
            logger.LogInformation("Tenant {Identifier} already exists", TestTenantIdentifier);
            return;
        }

        var tenantGrain = clusterClient.GetGrain<ITenantGrain>(TestTenantIdentifier);
        var result = await tenantGrain.InitializeAsync(new CreateTenantRequest
        {
            Identifier = TestTenantIdentifier,
            Name = TestTenantName,
            DisplayName = TestTenantName,
            CreatorUserId = userId
        });

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create tenant: {result.Error}");
        }

        // Note: InitializeAsync already registers the tenant in the registry internally

        // Add user as tenant member
        await tenantGrain.AddMemberAsync(userId);

        // Update user's tenant memberships
        var userGrain = clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        await userGrain.AddTenantMembershipAsync(TestTenantIdentifier);

        logger.LogInformation("Created tenant {Name} with identifier {Identifier}", TestTenantName, TestTenantIdentifier);
    }

    private async Task<string> CreateClientAndGetSecretAsync(CancellationToken stoppingToken)
    {
        var clientGrainKey = $"{TestTenantIdentifier}/{TestClientId}";
        var clientGrain = clusterClient.GetGrain<IClientGrain>(clientGrainKey);

        if (await clientGrain.ExistsAsync())
        {
            logger.LogInformation("Client {ClientId} already exists, updating settings and secret", TestClientId);

            // Update UserFlow to ensure organizations are disabled
            await clientGrain.UpdateAsync(new UpdateClientRequest
            {
                UserFlow = new UserFlowSettings
                {
                    OrganizationsEnabled = false,
                    OrganizationMode = OrganizationRegistrationMode.None
                }
            });

            // ⚠️ TESTING ONLY: Set a hardcoded secret for deterministic example/testing scenarios
            await clientGrain.AddKnownClientSecret_TESTING_ONLY(TestClientSecret, "Hardcoded testing secret");

            logger.LogInformation("Updated client settings and set known secret (TESTING ONLY)");
            return TestClientSecret;
        }

        var result = await clientGrain.CreateAsync(new CreateClientRequest
        {
            TenantId = TestTenantIdentifier,
            ClientId = TestClientId,
            ClientName = TestClientName,
            Description = "Example web application for testing OAuth2/OIDC flows",
            IsConfidential = true,  // Confidential client for CCF support
            RequirePkce = true,
            RequireConsent = false,
            RedirectUris =
            [
                "http://localhost:5200/callback",
                "http://localhost:5200/signin-oidc",
                "https://localhost:7200/callback",
                "https://localhost:7200/signin-oidc"
            ],
            PostLogoutRedirectUris =
            [
                "http://localhost:5200/",
                "https://localhost:7200/",
                "http://localhost:5200/signout-callback-oidc",
                "https://localhost:7200/signout-callback-oidc"
            ],
            AllowedScopes = ["openid", "profile", "email"],
            AllowedGrantTypes = ["authorization_code", "refresh_token", "client_credentials"],
            CorsOrigins =
            [
                "http://localhost:5200",
                "https://localhost:7200"
            ],
            UserFlow = new UserFlowSettings
            {
                OrganizationsEnabled = false,
                OrganizationMode = OrganizationRegistrationMode.None
            }
        });

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create client: {result.Error}");
        }

        // ⚠️ TESTING ONLY: Set a hardcoded secret for deterministic example/testing scenarios
        await clientGrain.AddKnownClientSecret_TESTING_ONLY(TestClientSecret, "Hardcoded testing secret");

        logger.LogInformation("Client secret set for new client (TESTING ONLY)");

        // Register client with tenant
        var tenantGrain = clusterClient.GetGrain<ITenantGrain>(TestTenantIdentifier);
        await tenantGrain.AddClientAsync(TestClientId);

        logger.LogInformation("Created confidential client {ClientId} for tenant {TenantId}", TestClientId, TestTenantIdentifier);

        return TestClientSecret;
    }

    private static string HashPassword(string password)
    {
        const int iterations = 100000;
        const int saltSize = 16;
        const int hashSize = 32;

        var salt = RandomNumberGenerator.GetBytes(saltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            hashSize);

        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}

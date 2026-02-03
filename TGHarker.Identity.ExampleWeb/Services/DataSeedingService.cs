using System.Security.Cryptography;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.ExampleWeb.Services;

public sealed class DataSeedingService : BackgroundService
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<DataSeedingService> _logger;

    private const string TestEmail = "test@test.com";
    private const string TestPassword = "Password!23";
    private const string TestTenantIdentifier = "test";
    private const string TestTenantName = "Test";
    private const string TestClientId = "testweb";
    private const string TestClientName = "TestWeb";

    public DataSeedingService(IClusterClient clusterClient, ILogger<DataSeedingService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for Orleans cluster to be ready
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        try
        {
            _logger.LogInformation("Starting data seeding...");

            // Step 1: Create user
            var userId = await CreateUserAsync(stoppingToken);

            // Step 2: Create tenant
            await CreateTenantAsync(userId, stoppingToken);

            // Step 3: Create client/application
            await CreateClientAsync(stoppingToken);

            _logger.LogInformation("Data seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data seeding");
        }
    }

    private async Task<string> CreateUserAsync(CancellationToken stoppingToken)
    {
        var userRegistry = _clusterClient.GetGrain<IUserRegistryGrain>("user-registry");
        var existingUserId = await userRegistry.GetUserIdByEmailAsync(TestEmail);

        if (!string.IsNullOrEmpty(existingUserId))
        {
            _logger.LogInformation("User {Email} already exists with ID {UserId}", TestEmail, existingUserId);
            return existingUserId;
        }

        var userId = Guid.NewGuid().ToString();
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");

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

        _logger.LogInformation("Created user {Email} with ID {UserId}", TestEmail, userId);
        return userId;
    }

    private async Task CreateTenantAsync(string userId, CancellationToken stoppingToken)
    {
        var tenantRegistry = _clusterClient.GetGrain<ITenantRegistryGrain>("tenant-registry");
        var existingTenantId = await tenantRegistry.GetTenantIdByIdentifierAsync(TestTenantIdentifier);

        if (!string.IsNullOrEmpty(existingTenantId))
        {
            _logger.LogInformation("Tenant {Identifier} already exists", TestTenantIdentifier);
            return;
        }

        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(TestTenantIdentifier);
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
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        await userGrain.AddTenantMembershipAsync(TestTenantIdentifier);

        _logger.LogInformation("Created tenant {Name} with identifier {Identifier}", TestTenantName, TestTenantIdentifier);
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
            Description = "Example web application for testing OAuth2/OIDC flows",
            IsConfidential = false,
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
                "https://localhost:7200/"
            ],
            AllowedScopes = ["openid", "profile", "email"],
            AllowedGrantTypes = ["authorization_code", "refresh_token"],
            CorsOrigins =
            [
                "http://localhost:5200",
                "https://localhost:7200"
            ]
        });

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create client: {result.Error}");
        }

        // Register client with tenant
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(TestTenantIdentifier);
        await tenantGrain.AddClientAsync(TestClientId);

        _logger.LogInformation("Created client {ClientId} for tenant {TenantId}", TestClientId, TestTenantIdentifier);
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

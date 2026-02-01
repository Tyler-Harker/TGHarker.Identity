using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;
using TGharker.Identity.Web.Services;

namespace TGHarker.Identity.IntegrationTests.Infrastructure;

public class TestDataBuilder
{
    private readonly IClusterClient _clusterClient;
    private readonly TestGrainSearchService _searchService;
    private readonly IPasswordHasher _passwordHasher;

    public TestDataBuilder(IClusterClient clusterClient, TestGrainSearchService searchService)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
        _passwordHasher = new PasswordHasher();
    }

    public async Task<TenantState> CreateTenantAsync(
        string identifier = "test-tenant",
        string name = "Test Tenant")
    {
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(identifier);

        var result = await tenantGrain.InitializeAsync(new CreateTenantRequest
        {
            Identifier = identifier,
            Name = name,
            CreatorUserId = "system"
        });

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create tenant: {result.Error}");
        }

        // Register tenant in search service
        _searchService.RegisterTenant(identifier, result.TenantId!);

        // Set up signing keys for the tenant
        var signingKeyGrain = _clusterClient.GetGrain<ISigningKeyGrain>($"{result.TenantId}/signing-keys");
        await signingKeyGrain.GenerateNewKeyAsync();

        var state = await tenantGrain.GetStateAsync();
        return state ?? throw new InvalidOperationException("Tenant state is null after creation");
    }

    public async Task<(ClientState Client, string? Secret)> CreateClientAsync(
        string tenantId,
        string clientName = "Test Client",
        string clientIdentifier = "test-client",
        bool isConfidential = false,
        string[]? redirectUris = null,
        string[]? allowedScopes = null,
        UserFlowSettings? userFlowSettings = null)
    {
        var clientGrainKey = $"{tenantId}/{clientIdentifier}";
        var clientGrain = _clusterClient.GetGrain<IClientGrain>(clientGrainKey);

        redirectUris ??= ["https://localhost:5001/signin-oidc"];
        allowedScopes ??= ["openid", "profile", "email"];

        var result = await clientGrain.CreateAsync(new CreateClientRequest
        {
            TenantId = tenantId,
            ClientId = clientIdentifier,
            ClientName = clientName,
            IsConfidential = isConfidential,
            RedirectUris = redirectUris.ToList(),
            PostLogoutRedirectUris = ["https://localhost:5001/signout-callback-oidc"],
            AllowedScopes = allowedScopes.ToList(),
            AllowedGrantTypes = ["authorization_code", "refresh_token"],
            RequirePkce = true,
            UserFlow = userFlowSettings ?? new UserFlowSettings
            {
                OrganizationsEnabled = false,
                OrganizationMode = OrganizationRegistrationMode.None
            }
        });

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create client: {result.Error}");
        }

        // Register client in search service
        _searchService.RegisterClient(tenantId, clientIdentifier, clientGrainKey);

        // Register client with tenant
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        await tenantGrain.AddClientAsync(clientIdentifier);

        var client = await clientGrain.GetStateAsync();
        return (client!, result.ClientSecret);
    }

    public async Task<UserState> CreateUserAsync(
        string tenantId,
        string email = "testuser@example.com",
        string password = "TestPassword123!",
        string? givenName = "Test",
        string? familyName = "User")
    {
        var userId = Guid.NewGuid().ToString();

        // Lock the email first
        var emailLock = _clusterClient.GetGrain<IUserEmailLockGrain>(email.ToLowerInvariant());
        var lockResult = await emailLock.TryLockAsync(userId);
        if (!lockResult.Success)
        {
            throw new InvalidOperationException($"Email {email} is already in use: {lockResult.Error}");
        }

        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");

        // Hash the password before storing
        var hashedPassword = _passwordHasher.Hash(password);

        var createResult = await userGrain.CreateAsync(new CreateUserRequest
        {
            Email = email,
            Password = hashedPassword,
            GivenName = givenName,
            FamilyName = familyName
        });

        if (!createResult.Success)
        {
            throw new InvalidOperationException($"Failed to create user: {createResult.Error}");
        }

        // Register user in search service
        _searchService.RegisterUser(email, userId);

        // Add user to tenant
        await userGrain.AddTenantMembershipAsync(tenantId);

        // Create tenant membership
        var membershipGrain = _clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
        await membershipGrain.CreateAsync(new CreateMembershipRequest
        {
            UserId = userId,
            TenantId = tenantId,
            Roles = ["user"]
        });

        // Add user to tenant member list
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        await tenantGrain.AddMemberAsync(userId);

        var state = await userGrain.GetStateAsync();
        return state ?? throw new InvalidOperationException("User state is null after creation");
    }

    public async Task<OrganizationState> CreateOrganizationAsync(
        string tenantId,
        string userId,
        string name = "Test Organization",
        string identifier = "test-org")
    {
        var orgId = Guid.NewGuid().ToString();
        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{orgId}");

        var result = await orgGrain.InitializeAsync(new CreateOrganizationRequest
        {
            TenantId = tenantId,
            Identifier = identifier,
            Name = name,
            CreatorUserId = userId,
            Settings = new OrganizationSettings
            {
                AllowSelfRegistration = false,
                RequireEmailVerification = true
            }
        });

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create organization: {result.Error}");
        }

        // Register organization in search service
        _searchService.RegisterOrganization(tenantId, identifier, orgId);

        // Add user as owner
        var membershipGrain = _clusterClient.GetGrain<IOrganizationMembershipGrain>(
            $"{tenantId}/org-{orgId}/member-{userId}");

        await membershipGrain.CreateAsync(new CreateOrganizationMembershipRequest
        {
            UserId = userId,
            OrganizationId = orgId,
            TenantId = tenantId,
            Roles = [WellKnownOrganizationRoles.Owner]
        });

        await orgGrain.AddMemberAsync(userId);

        // Add organization to user's membership list
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        await userGrain.AddOrganizationMembershipAsync(tenantId, orgId);

        // Add organization to tenant
        var tenantGrain = _clusterClient.GetGrain<ITenantGrain>(tenantId);
        await tenantGrain.AddOrganizationAsync(orgId);

        var state = await orgGrain.GetStateAsync();
        return state ?? throw new InvalidOperationException("Organization state is null after creation");
    }
}

using System.Text.Json.Serialization;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGharker.Identity.Web.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants")
            .WithTags("Tenants");

        group.MapPost("/", CreateTenantAsync)
            .RequireAuthorization()
            .WithName("CreateTenant");

        group.MapGet("/{identifier}", GetTenantAsync)
            .AllowAnonymous()
            .WithName("GetTenant");

        group.MapPost("/{identifier}/members", AddMemberAsync)
            .RequireAuthorization()
            .WithName("AddTenantMember");

        group.MapPost("/{identifier}/clients", CreateClientAsync)
            .RequireAuthorization()
            .WithName("CreateClient");

        return endpoints;
    }

    private static async Task<IResult> CreateTenantAsync(
        HttpContext context,
        CreateTenantApiRequest request,
        IClusterClient clusterClient)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        // Check if tenant identifier already exists
        var registry = clusterClient.GetGrain<ITenantRegistryGrain>("tenant-registry");
        if (await registry.TenantExistsAsync(request.Identifier))
        {
            return Results.Conflict(new { error = "tenant_exists", error_description = "Tenant identifier already in use" });
        }

        // Create tenant (user becomes owner automatically)
        var tenantId = Guid.NewGuid().ToString();
        var tenantGrain = clusterClient.GetGrain<ITenantGrain>(tenantId);

        var result = await tenantGrain.InitializeAsync(new CreateTenantRequest
        {
            Identifier = request.Identifier,
            Name = request.Name,
            DisplayName = request.DisplayName,
            CreatorUserId = userId,
            Configuration = request.Configuration
        });

        if (!result.Success)
        {
            return Results.BadRequest(new { error = "creation_failed", error_description = result.Error });
        }

        return Results.Created($"/api/tenants/{request.Identifier}", new
        {
            id = result.TenantId,
            identifier = request.Identifier,
            name = request.Name
        });
    }

    private static async Task<IResult> GetTenantAsync(
        string identifier,
        IClusterClient clusterClient)
    {
        var registry = clusterClient.GetGrain<ITenantRegistryGrain>("tenant-registry");
        var tenantId = await registry.GetTenantIdByIdentifierAsync(identifier);

        if (string.IsNullOrEmpty(tenantId))
            return Results.NotFound();

        var tenantGrain = clusterClient.GetGrain<ITenantGrain>(tenantId);
        var state = await tenantGrain.GetStateAsync();

        if (state == null)
            return Results.NotFound();

        return Results.Ok(new TenantResponse
        {
            Id = state.Id,
            Identifier = state.Identifier,
            Name = state.Name,
            DisplayName = state.DisplayName,
            IsActive = state.IsActive
        });
    }

    private static async Task<IResult> AddMemberAsync(
        string identifier,
        AddMemberRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var currentUserId = context.User.FindFirst("sub")?.Value;
        var currentTenantId = context.User.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrEmpty(currentUserId))
            return Results.Unauthorized();

        // Get tenant
        var registry = clusterClient.GetGrain<ITenantRegistryGrain>("tenant-registry");
        var tenantId = await registry.GetTenantIdByIdentifierAsync(identifier);

        if (string.IsNullOrEmpty(tenantId))
            return Results.NotFound();

        // Check if current user is admin
        var currentMembershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{currentUserId}");
        var isAdmin = await currentMembershipGrain.HasRoleAsync(WellKnownRoles.TenantAdmin) ||
                      await currentMembershipGrain.HasRoleAsync(WellKnownRoles.TenantOwner);

        if (!isAdmin)
            return Results.Forbid();

        // Get user by email
        var userRegistry = clusterClient.GetGrain<IUserRegistryGrain>("user-registry");
        var userId = await userRegistry.GetUserIdByEmailAsync(request.Email);

        if (string.IsNullOrEmpty(userId))
            return Results.NotFound(new { error = "user_not_found" });

        // Check if already a member
        var membershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
        if (await membershipGrain.ExistsAsync())
            return Results.Conflict(new { error = "already_member" });

        // Create membership
        await membershipGrain.CreateAsync(new CreateMembershipRequest
        {
            UserId = userId,
            TenantId = tenantId,
            Roles = request.Roles?.ToList() ?? [WellKnownRoles.User]
        });

        // Add tenant to user's memberships
        var userGrain = clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        await userGrain.AddTenantMembershipAsync(tenantId);

        // Add user to tenant's member list
        var tenantGrain = clusterClient.GetGrain<ITenantGrain>(tenantId);
        await tenantGrain.AddMemberAsync(userId);

        return Results.Created($"/api/tenants/{identifier}/members/{userId}", new { userId, tenantId });
    }

    private static async Task<IResult> CreateClientAsync(
        string identifier,
        CreateClientApiRequest request,
        HttpContext context,
        IClusterClient clusterClient)
    {
        var currentUserId = context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(currentUserId))
            return Results.Unauthorized();

        // Get tenant
        var registry = clusterClient.GetGrain<ITenantRegistryGrain>("tenant-registry");
        var tenantId = await registry.GetTenantIdByIdentifierAsync(identifier);

        if (string.IsNullOrEmpty(tenantId))
            return Results.NotFound();

        // Check if current user is admin
        var membershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{currentUserId}");
        var isAdmin = await membershipGrain.HasRoleAsync(WellKnownRoles.TenantAdmin) ||
                      await membershipGrain.HasRoleAsync(WellKnownRoles.TenantOwner);

        if (!isAdmin)
            return Results.Forbid();

        // Create client
        var clientGrain = clusterClient.GetGrain<IClientGrain>($"{tenantId}/{request.ClientId}");

        if (await clientGrain.ExistsAsync())
            return Results.Conflict(new { error = "client_exists" });

        var result = await clientGrain.CreateAsync(new CreateClientRequest
        {
            TenantId = tenantId,
            ClientId = request.ClientId,
            ClientName = request.ClientName,
            Description = request.Description,
            IsConfidential = request.IsConfidential,
            RequireConsent = request.RequireConsent,
            RequirePkce = request.RequirePkce,
            RedirectUris = request.RedirectUris?.ToList() ?? [],
            AllowedScopes = request.AllowedScopes?.ToList() ?? [],
            AllowedGrantTypes = request.AllowedGrantTypes?.ToList() ?? [],
            CorsOrigins = request.CorsOrigins?.ToList() ?? [],
            PostLogoutRedirectUris = request.PostLogoutRedirectUris?.ToList() ?? []
        });

        if (!result.Success)
        {
            return Results.BadRequest(new { error = "creation_failed", error_description = result.Error });
        }

        return Results.Created($"/api/tenants/{identifier}/clients/{request.ClientId}", new
        {
            clientId = request.ClientId,
            clientSecret = result.ClientSecret // Only returned once!
        });
    }
}

public sealed class CreateTenantApiRequest
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("configuration")]
    public TenantConfiguration? Configuration { get; set; }
}

public sealed class TenantResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

public sealed class AddMemberRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("roles")]
    public string[]? Roles { get; set; }
}

public sealed class CreateClientApiRequest
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_confidential")]
    public bool IsConfidential { get; set; }

    [JsonPropertyName("require_consent")]
    public bool RequireConsent { get; set; }

    [JsonPropertyName("require_pkce")]
    public bool RequirePkce { get; set; } = true;

    [JsonPropertyName("redirect_uris")]
    public string[]? RedirectUris { get; set; }

    [JsonPropertyName("allowed_scopes")]
    public string[]? AllowedScopes { get; set; }

    [JsonPropertyName("allowed_grant_types")]
    public string[]? AllowedGrantTypes { get; set; }

    [JsonPropertyName("cors_origins")]
    public string[]? CorsOrigins { get; set; }

    [JsonPropertyName("post_logout_redirect_uris")]
    public string[]? PostLogoutRedirectUris { get; set; }
}

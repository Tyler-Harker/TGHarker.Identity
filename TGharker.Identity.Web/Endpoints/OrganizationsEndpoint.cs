using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Endpoints;

public static class OrganizationsEndpoint
{
    public static IEndpointRouteBuilder MapOrganizationsEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // Organizations endpoint - lists organizations the user has access to within the tenant
        endpoints.MapGet("/connect/organizations", HandleOrganizationsRequest)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("Organizations")
            .WithTags("API");

        // Tenant-prefixed route: /tenant/{tenantId}/connect/organizations
        endpoints.MapGet("/tenant/{tenantId}/connect/organizations", HandleOrganizationsRequest)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("OrganizationsWithTenant")
            .WithTags("API");

        return endpoints;
    }

    private static async Task<IResult> HandleOrganizationsRequest(
        HttpContext context,
        IClusterClient clusterClient,
        ILogger<Program> logger)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        var tenantId = context.User.FindFirst("tenant_id")?.Value;

        logger.LogInformation("Organizations request - UserId: {UserId}, TenantId: {TenantId}",
            userId ?? "(null)", tenantId ?? "(null)");

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            return Results.Json(new { error = "unauthorized", error_description = "Missing user or tenant information" }, statusCode: 401);
        }

        // Get user's organization memberships in this tenant
        var userGrain = clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var memberships = await userGrain.GetOrganizationMembershipsInTenantAsync(tenantId);

        var organizations = new List<OrganizationResponse>();

        foreach (var membership in memberships)
        {
            var orgGrain = clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{membership.OrganizationId}");
            var orgState = await orgGrain.GetStateAsync();

            if (orgState != null && orgState.IsActive)
            {
                // Get the user's membership details for this organization
                var membershipGrain = clusterClient.GetGrain<IOrganizationMembershipGrain>(
                    $"{tenantId}/org-{membership.OrganizationId}/member-{userId}");
                var membershipState = await membershipGrain.GetStateAsync();

                organizations.Add(new OrganizationResponse
                {
                    Id = orgState.Id,
                    Identifier = orgState.Identifier,
                    Name = orgState.Name,
                    DisplayName = orgState.DisplayName,
                    Description = orgState.Description,
                    Roles = membershipState?.Roles ?? [],
                    JoinedAt = membershipState?.JoinedAt
                });
            }
        }

        logger.LogInformation("Organizations request successful - returning {Count} organizations for user {UserId}",
            organizations.Count, userId);

        return Results.Json(new OrganizationsResponse { Organizations = organizations });
    }
}

public sealed class OrganizationsResponse
{
    [JsonPropertyName("organizations")]
    public List<OrganizationResponse> Organizations { get; set; } = [];
}

public sealed class OrganizationResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("identifier")]
    public string Identifier { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = [];

    [JsonPropertyName("joined_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? JoinedAt { get; set; }
}

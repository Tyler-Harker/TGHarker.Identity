using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Endpoints;

public static class UserInfoEndpoint
{
    public static IEndpointRouteBuilder MapUserInfoEndpoint(this IEndpointRouteBuilder endpoints)
    {
        // UserInfo endpoint must use JWT bearer authentication (not cookie)
        // This prevents redirect to login page when auth fails
        endpoints.MapGet("/connect/userinfo", HandleUserInfoRequest)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("UserInfoGet")
            .WithTags("OIDC");

        endpoints.MapPost("/connect/userinfo", HandleUserInfoRequest)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("UserInfoPost")
            .WithTags("OIDC");

        // Tenant-prefixed routes: /tenant/{tenantId}/connect/userinfo
        endpoints.MapGet("/tenant/{tenantId}/connect/userinfo", HandleUserInfoRequest)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("UserInfoGetWithTenant")
            .WithTags("OIDC");

        endpoints.MapPost("/tenant/{tenantId}/connect/userinfo", HandleUserInfoRequest)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser())
            .WithName("UserInfoPostWithTenant")
            .WithTags("OIDC");

        return endpoints;
    }

    private static async Task<IResult> HandleUserInfoRequest(
        HttpContext context,
        IClusterClient clusterClient,
        ILogger<Program> logger)
    {
        logger.LogInformation("UserInfo request received. Path: {Path}, IsAuthenticated: {IsAuthenticated}",
            context.Request.Path,
            context.User.Identity?.IsAuthenticated ?? false);

        var userId = context.User.FindFirst("sub")?.Value;
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        var scopeClaim = context.User.FindFirst("scope")?.Value;

        logger.LogInformation("UserInfo claims - UserId: {UserId}, TenantId: {TenantId}, Scope: {Scope}",
            userId ?? "(null)", tenantId ?? "(null)", scopeClaim ?? "(null)");

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(tenantId))
        {
            logger.LogWarning("UserInfo request unauthorized - missing userId or tenantId");
            return Results.Json(new { error = "unauthorized" }, statusCode: 401);
        }

        var scopes = scopeClaim?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];

        // Get user
        var userGrain = clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        var user = await userGrain.GetStateAsync();

        if (user == null)
            return Results.Unauthorized();

        // Get membership
        var membershipGrain = clusterClient.GetGrain<ITenantMembershipGrain>($"{tenantId}/member-{userId}");
        var membership = await membershipGrain.GetStateAsync();

        var claims = new UserInfoResponse
        {
            Sub = userId
        };

        if (scopes.Contains(StandardScopes.Email))
        {
            claims.Email = user.Email;
            claims.EmailVerified = user.EmailVerified;
        }

        if (scopes.Contains(StandardScopes.Profile))
        {
            claims.GivenName = user.GivenName;
            claims.FamilyName = user.FamilyName;
            claims.Picture = user.Picture;
            claims.PreferredUsername = membership?.Username;

            if (!string.IsNullOrEmpty(user.GivenName) || !string.IsNullOrEmpty(user.FamilyName))
                claims.Name = $"{user.GivenName} {user.FamilyName}".Trim();
        }

        if (scopes.Contains(StandardScopes.Phone))
        {
            claims.PhoneNumber = user.PhoneNumber;
            claims.PhoneNumberVerified = user.PhoneNumberVerified;
        }

        logger.LogInformation("UserInfo request successful for user {UserId}", userId);
        return Results.Json(claims, contentType: "application/json");
    }
}

public sealed class UserInfoResponse
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("given_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GivenName { get; set; }

    [JsonPropertyName("family_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FamilyName { get; set; }

    [JsonPropertyName("preferred_username")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreferredUsername { get; set; }

    [JsonPropertyName("picture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Picture { get; set; }

    [JsonPropertyName("email")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; set; }

    [JsonPropertyName("email_verified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EmailVerified { get; set; }

    [JsonPropertyName("phone_number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("phone_number_verified")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PhoneNumberVerified { get; set; }
}

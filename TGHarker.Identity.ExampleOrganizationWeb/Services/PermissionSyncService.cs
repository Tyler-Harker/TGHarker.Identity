using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TGHarker.Identity.Client;

namespace TGHarker.Identity.ExampleOrganizationWeb.Services;

/// <summary>
/// Syncs application permissions and roles using the TGHarker.Identity.Client SDK.
/// Waits for the client secret to be available (set by DataSeedingService) before syncing.
/// </summary>
public sealed class PermissionSyncService(
    ClientSecretStore secretStore,
    IdentityClientBuilder builder,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<PermissionSyncService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the DataSeedingService to create the client and set the secret
        logger.LogInformation("Waiting for client secret to be available...");

        string clientSecret;
        try
        {
            clientSecret = await secretStore.GetSecretAsync(stoppingToken);
            logger.LogInformation("Client secret received, starting permission sync");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Permission sync cancelled while waiting for client secret");
            return;
        }

        // Wait a bit more for the identity server to be fully ready
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        // Get configuration
        var authority = configuration["Identity:Authority"] ?? "https://localhost:7157";
        var tenantId = configuration["Identity:TenantId"] ?? "test";
        var clientId = "testorgweb";

        // First, get an access token via Client Credentials Flow (using tenant-prefixed endpoint)
        string? accessToken;
        try
        {
            accessToken = await GetAccessTokenAsync(authority, tenantId, clientId, clientSecret, stoppingToken);
            if (accessToken == null)
            {
                logger.LogError("Failed to obtain access token for permission sync");
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error obtaining access token for permission sync");
            return;
        }

        // Now sync permissions using the SDK's configured permissions
        await SyncPermissionsAsync(authority, clientId, accessToken, stoppingToken);
        await SyncRolesAsync(authority, clientId, accessToken, stoppingToken);

        logger.LogInformation("Permission sync completed via SDK");
    }

    private HttpClient CreateHttpClient()
    {
        if (environment.IsDevelopment())
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            return new HttpClient(handler);
        }
        return new HttpClient();
    }

    private async Task<string?> GetAccessTokenAsync(string authority, string tenantId, string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        using var httpClient = CreateHttpClient();

        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "openid"
        };

        // Use tenant-prefixed token endpoint to get token with tenant_id claim
        using var content = new FormUrlEncodedContent(tokenRequest);
        var response = await httpClient.PostAsync($"{authority}/tenant/{tenantId}/connect/token", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Token request failed: {StatusCode} - {Error}", response.StatusCode, error);
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogDebug("Token response: {Response}", responseContent);

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, JsonOptions);
        if (tokenResponse?.AccessToken == null)
        {
            logger.LogError("Failed to deserialize token response or access_token is null");
            return null;
        }

        logger.LogInformation("Successfully obtained access token");
        return tokenResponse.AccessToken;
    }

    private async Task SyncPermissionsAsync(string authority, string clientId, string accessToken, CancellationToken cancellationToken)
    {
        using var httpClient = CreateHttpClient();

        var permissions = builder.Permissions.Select(p => new
        {
            name = p.Name,
            display_name = p.DisplayName,
            description = p.Description
        }).ToList();

        logger.LogInformation("Syncing {Count} permissions via SDK: {Permissions}",
            permissions.Count, string.Join(", ", permissions.Select(p => p.name)));

        var request = new HttpRequestMessage(HttpMethod.Put, $"{authority}/api/clients/{clientId}/system/permissions")
        {
            Content = JsonContent.Create(new { permissions }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<SyncResultResponse>(JsonOptions, cancellationToken);
            logger.LogInformation("Permissions synced: {Added} added, {Updated} updated, {Removed} removed",
                result?.Added ?? 0, result?.Updated ?? 0, result?.Removed ?? 0);
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Permission sync failed: {StatusCode} - {Error}", response.StatusCode, error);
        }
    }

    private async Task SyncRolesAsync(string authority, string clientId, string accessToken, CancellationToken cancellationToken)
    {
        using var httpClient = CreateHttpClient();

        var roles = builder.Roles.Select(r => new
        {
            id = r.Id ?? $"role-{r.Name}",
            name = r.Name,
            display_name = r.DisplayName,
            description = r.Description,
            permissions = r.Permissions.ToArray()
        }).ToList();

        logger.LogInformation("Syncing {Count} roles via SDK: {Roles}",
            roles.Count, string.Join(", ", roles.Select(r => r.name)));

        var request = new HttpRequestMessage(HttpMethod.Put, $"{authority}/api/clients/{clientId}/system/roles")
        {
            Content = JsonContent.Create(new { roles }, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<SyncResultResponse>(JsonOptions, cancellationToken);
            logger.LogInformation("Roles synced: {Added} added, {Updated} updated, {Removed} removed",
                result?.Added ?? 0, result?.Updated ?? 0, result?.Removed ?? 0);
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Role sync failed: {StatusCode} - {Error}", response.StatusCode, error);
        }
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
    private sealed record SyncResultResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("success")] bool Success,
        [property: System.Text.Json.Serialization.JsonPropertyName("added")] int Added,
        [property: System.Text.Json.Serialization.JsonPropertyName("updated")] int Updated,
        [property: System.Text.Json.Serialization.JsonPropertyName("removed")] int Removed,
        [property: System.Text.Json.Serialization.JsonPropertyName("error")] string? Error);
}

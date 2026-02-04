using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TGHarker.Identity.Client;

/// <summary>
/// Implementation of the identity client.
/// </summary>
public sealed class IdentityClient : IIdentityClient
{
    private readonly HttpClient _httpClient;
    private readonly IdentityClientOptions _options;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public IdentityClient(HttpClient httpClient, IOptions<IdentityClientOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (string.IsNullOrEmpty(_options.Authority))
            throw new ArgumentException("Authority is required", nameof(options));
        if (string.IsNullOrEmpty(_options.ClientId))
            throw new ArgumentException("ClientId is required", nameof(options));
        if (string.IsNullOrEmpty(_options.ClientSecret))
            throw new ArgumentException("ClientSecret is required", nameof(options));
    }

    public async Task<SyncResult> SyncPermissionsAsync(IEnumerable<Permission> permissions, CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        var request = new SyncPermissionsRequest
        {
            Permissions = permissions.Select(p => new SyncPermissionItem
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Description = p.Description
            }).ToList()
        };

        var url = $"{_options.Authority.TrimEnd('/')}/api/clients/{_options.ClientId}/system/permissions";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return SyncResult.Failed($"HTTP {(int)response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<SyncResultResponse>(JsonOptions, cancellationToken);
        if (result == null)
            return SyncResult.Failed("Failed to parse response");

        return new SyncResult
        {
            Success = result.Success,
            Added = result.Added,
            Updated = result.Updated,
            Removed = result.Removed,
            Error = result.Error
        };
    }

    public async Task<SyncResult> SyncRolesAsync(IEnumerable<Role> roles, CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);

        var request = new SyncRolesRequest
        {
            Roles = roles.Select(r => new SyncRoleItem
            {
                Id = r.Id,
                Name = r.Name,
                DisplayName = r.DisplayName,
                Description = r.Description,
                Permissions = r.Permissions.ToArray()
            }).ToList()
        };

        var url = $"{_options.Authority.TrimEnd('/')}/api/clients/{_options.ClientId}/system/roles";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Put, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return SyncResult.Failed($"HTTP {(int)response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<SyncResultResponse>(JsonOptions, cancellationToken);
        if (result == null)
            return SyncResult.Failed("Failed to parse response");

        return new SyncResult
        {
            Success = result.Success,
            Added = result.Added,
            Updated = result.Updated,
            Removed = result.Removed,
            Error = result.Error
        };
    }

    public async Task<(SyncResult Permissions, SyncResult Roles)> SyncAllAsync(
        IEnumerable<Permission> permissions,
        IEnumerable<Role> roles,
        CancellationToken cancellationToken = default)
    {
        var permissionsResult = await SyncPermissionsAsync(permissions, cancellationToken);
        var rolesResult = await SyncRolesAsync(roles, cancellationToken);
        return (permissionsResult, rolesResult);
    }

    private async Task EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            return;

        var tokenEndpoint = $"{_options.Authority.TrimEnd('/')}/connect/token";

        var tokenRequest = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["scope"] = _options.Scope
        };

        using var content = new FormUrlEncodedContent(tokenRequest);
        var response = await _httpClient.PostAsync(tokenEndpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to obtain access token: {errorContent}");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken);
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            throw new InvalidOperationException("Invalid token response");

        _accessToken = tokenResponse.AccessToken;
        // Set expiry slightly before actual expiry to account for clock skew
        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
    }

    #region Request/Response DTOs

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class SyncPermissionsRequest
    {
        [JsonPropertyName("permissions")]
        public List<SyncPermissionItem> Permissions { get; set; } = [];
    }

    private sealed class SyncPermissionItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    private sealed class SyncRolesRequest
    {
        [JsonPropertyName("roles")]
        public List<SyncRoleItem> Roles { get; set; } = [];
    }

    private sealed class SyncRoleItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("permissions")]
        public string[]? Permissions { get; set; }
    }

    private sealed class SyncResultResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("added")]
        public int Added { get; set; }

        [JsonPropertyName("updated")]
        public int Updated { get; set; }

        [JsonPropertyName("removed")]
        public int Removed { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    #endregion
}

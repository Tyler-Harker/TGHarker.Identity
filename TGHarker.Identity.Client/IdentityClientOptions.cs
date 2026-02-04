namespace TGHarker.Identity.Client;

/// <summary>
/// Configuration options for the TGHarker.Identity client.
/// </summary>
public sealed class IdentityClientOptions
{
    /// <summary>
    /// The base URL of the identity server (e.g., "https://identity.example.com").
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The client ID of this application.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The client secret for authenticating with the identity server.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The tenant ID this client belongs to.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Scope to request when obtaining an access token. Defaults to "openid".
    /// </summary>
    public string Scope { get; set; } = "openid";

    /// <summary>
    /// Whether to automatically sync permissions and roles on application startup.
    /// Defaults to true.
    /// </summary>
    public bool SyncOnStartup { get; set; } = true;
}

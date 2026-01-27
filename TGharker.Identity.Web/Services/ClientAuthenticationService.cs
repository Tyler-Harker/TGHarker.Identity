using System.Text;
using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Services;

public sealed class ClientAuthenticationService : IClientAuthenticationService
{
    private readonly IClusterClient _clusterClient;

    public ClientAuthenticationService(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public async Task<ClientAuthenticationResult> AuthenticateAsync(HttpContext context, string tenantId)
    {
        string? clientId = null;
        string? clientSecret = null;

        // Try Basic authentication header first
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authValue = authHeader.ToString();
            if (authValue.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var credentials = Encoding.UTF8.GetString(
                        Convert.FromBase64String(authValue["Basic ".Length..]));
                    var colonIndex = credentials.IndexOf(':');

                    if (colonIndex > 0)
                    {
                        clientId = Uri.UnescapeDataString(credentials[..colonIndex]);
                        clientSecret = Uri.UnescapeDataString(credentials[(colonIndex + 1)..]);
                    }
                }
                catch
                {
                    return ClientAuthenticationResult.Failure("invalid_client", "Invalid Basic authentication header");
                }
            }
        }

        // Fall back to form parameters (client_secret_post)
        if (string.IsNullOrEmpty(clientId))
        {
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync();
                clientId = form["client_id"].FirstOrDefault();
                clientSecret = form["client_secret"].FirstOrDefault();
            }
        }

        if (string.IsNullOrEmpty(clientId))
        {
            return ClientAuthenticationResult.Failure("invalid_client", "Client ID not provided");
        }

        // Get client from grain
        var clientGrain = _clusterClient.GetGrain<IClientGrain>($"{tenantId}/{clientId}");
        var clientState = await clientGrain.GetStateAsync();

        if (clientState == null)
        {
            return ClientAuthenticationResult.Failure("invalid_client", "Client not found");
        }

        if (!clientState.IsActive)
        {
            return ClientAuthenticationResult.Failure("invalid_client", "Client is not active");
        }

        // Validate secret for confidential clients
        if (clientState.IsConfidential)
        {
            if (string.IsNullOrEmpty(clientSecret))
            {
                return ClientAuthenticationResult.Failure("invalid_client", "Client secret required");
            }

            var isValidSecret = await clientGrain.ValidateSecretAsync(clientSecret);
            if (!isValidSecret)
            {
                return ClientAuthenticationResult.Failure("invalid_client", "Invalid client credentials");
            }
        }

        return ClientAuthenticationResult.Success(clientState);
    }
}

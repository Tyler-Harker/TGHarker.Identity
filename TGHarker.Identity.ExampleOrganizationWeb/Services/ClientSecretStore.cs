namespace TGHarker.Identity.ExampleOrganizationWeb.Services;

/// <summary>
/// Stores the client secret generated during data seeding so the identity client can use it.
/// In a real application, secrets would come from secure configuration (Key Vault, env vars, etc.)
/// </summary>
public sealed class ClientSecretStore
{
    private readonly TaskCompletionSource<string> _secretTcs = new();

    /// <summary>
    /// Sets the client secret. Called by DataSeedingService after creating the client.
    /// </summary>
    public void SetSecret(string secret)
    {
        _secretTcs.TrySetResult(secret);
    }

    /// <summary>
    /// Gets the client secret, waiting if necessary for it to be set.
    /// </summary>
    public Task<string> GetSecretAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.Register(() => _secretTcs.TrySetCanceled(cancellationToken));
        return _secretTcs.Task;
    }

    /// <summary>
    /// Returns true if the secret has been set.
    /// </summary>
    public bool HasSecret => _secretTcs.Task.IsCompletedSuccessfully;
}

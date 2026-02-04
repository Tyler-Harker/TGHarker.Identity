using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TGHarker.Identity.Client;

/// <summary>
/// Hosted service that syncs permissions and roles on application startup.
/// </summary>
internal sealed class IdentitySyncHostedService : IHostedService
{
    private readonly IIdentityClient _identityClient;
    private readonly IdentityClientOptions _options;
    private readonly IdentityClientBuilder _builder;
    private readonly ILogger<IdentitySyncHostedService> _logger;

    public IdentitySyncHostedService(
        IIdentityClient identityClient,
        IOptions<IdentityClientOptions> options,
        IdentityClientBuilder builder,
        ILogger<IdentitySyncHostedService> logger)
    {
        _identityClient = identityClient;
        _options = options.Value;
        _builder = builder;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.SyncOnStartup)
        {
            _logger.LogDebug("Identity sync on startup is disabled");
            return;
        }

        if (_builder.Permissions.Count == 0 && _builder.Roles.Count == 0)
        {
            _logger.LogDebug("No permissions or roles configured for sync");
            return;
        }

        _logger.LogInformation(
            "Syncing {PermissionCount} permissions and {RoleCount} roles with identity server",
            _builder.Permissions.Count,
            _builder.Roles.Count);

        try
        {
            var (permissionsResult, rolesResult) = await _identityClient.SyncAllAsync(
                _builder.Permissions,
                _builder.Roles,
                cancellationToken);

            if (permissionsResult.Success)
            {
                _logger.LogInformation(
                    "Permissions synced: {Added} added, {Updated} updated, {Removed} removed",
                    permissionsResult.Added,
                    permissionsResult.Updated,
                    permissionsResult.Removed);
            }
            else
            {
                _logger.LogError("Failed to sync permissions: {Error}", permissionsResult.Error);
            }

            if (rolesResult.Success)
            {
                _logger.LogInformation(
                    "Roles synced: {Added} added, {Updated} updated, {Removed} removed",
                    rolesResult.Added,
                    rolesResult.Updated,
                    rolesResult.Removed);
            }
            else
            {
                _logger.LogError("Failed to sync roles: {Error}", rolesResult.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing permissions and roles with identity server");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

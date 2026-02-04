using Microsoft.Extensions.DependencyInjection;

namespace TGHarker.Identity.Client;

/// <summary>
/// Extension methods for configuring identity client services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the TGHarker.Identity client services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure the identity client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTGHarkerIdentityClient(
        this IServiceCollection services,
        Action<IdentityClientOptions> configureOptions)
    {
        return services.AddTGHarkerIdentityClient(configureOptions, _ => { });
    }

    /// <summary>
    /// Adds the TGHarker.Identity client services to the service collection with permissions and roles configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure the identity client options.</param>
    /// <param name="configureBuilder">Action to configure permissions and roles.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTGHarkerIdentityClient(
        this IServiceCollection services,
        Action<IdentityClientOptions> configureOptions,
        Action<IdentityClientBuilder> configureBuilder)
    {
        services.Configure(configureOptions);

        var builder = new IdentityClientBuilder();
        configureBuilder(builder);
        services.AddSingleton(builder);

        services.AddHttpClient<IIdentityClient, IdentityClient>();
        services.AddHostedService<IdentitySyncHostedService>();

        return services;
    }
}

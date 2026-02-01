using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.TestingHost;
using TGHarker.Identity.Grains;
using Xunit;

namespace TGHarker.Identity.IntegrationTests.Infrastructure;

public class TestClusterFixture : IAsyncLifetime
{
    public TestCluster Cluster { get; private set; } = null!;
    public IClusterClient ClusterClient => Cluster.Client;

    public async Task InitializeAsync()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        builder.AddClientBuilderConfigurator<TestClientConfigurator>();

        Cluster = builder.Build();
        await Cluster.DeployAsync();
    }

    public async Task DisposeAsync()
    {
        await Cluster.StopAllSilosAsync();
    }

    private class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorage("Default");
            siloBuilder.AddMemoryGrainStorageAsDefault();

            // Register grain implementations
            siloBuilder.ConfigureServices(services =>
            {
                // Add any required services for grains
            });
        }
    }

    private class TestClientConfigurator : IClientBuilderConfigurator
    {
        public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
        {
            // Client configuration if needed
        }
    }
}

[CollectionDefinition(Name)]
public class ClusterCollection : ICollectionFixture<TestClusterFixture>
{
    public const string Name = "ClusterCollection";
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Orleans.TestingHost;
using TGHarker.Orleans.Search.PostgreSQL;

namespace TGHarker.Identity.IntegrationTests.Infrastructure;

public class IdentityWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly TestCluster _cluster;
    private readonly string _connectionString;

    public IdentityWebApplicationFactory(TestCluster cluster, string connectionString)
    {
        _cluster = cluster;
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Add the test cluster client
            services.AddSingleton<IClusterClient>(_cluster.Client);

            // Add PostgreSQL search context for testing
            services.AddDbContext<PostgreSqlSearchContext>(options =>
            {
                options.UseNpgsql(_connectionString);
            });
        });
    }
}

// Make Program accessible for WebApplicationFactory
public partial class Program { }

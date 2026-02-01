using Microsoft.Playwright;
using Orleans.TestingHost;
using Testcontainers.PostgreSql;
using Xunit;

namespace TGHarker.Identity.IntegrationTests.Infrastructure;

public class IntegrationTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgresContainer = null!;
    private TestCluster _cluster = null!;
    private IdentityTestServer _testServer = null!;
    private TestGrainSearchService _searchService = null!;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public IClusterClient ClusterClient => _cluster.Client;
    public string BaseAddress => _testServer.BaseAddress;
    public TestDataBuilder DataBuilder { get; private set; } = null!;
    public IBrowser Browser => _browser;

    public async Task InitializeAsync()
    {
        // Start PostgreSQL container
        _postgresContainer = new PostgreSqlBuilder("postgres:16")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await _postgresContainer.StartAsync();

        // Start Orleans test cluster
        var clusterBuilder = new TestClusterBuilder();
        clusterBuilder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        _cluster = clusterBuilder.Build();
        await _cluster.DeployAsync();

        // Create search service and data builder
        _searchService = new TestGrainSearchService(_cluster.Client);
        DataBuilder = new TestDataBuilder(_cluster.Client, _searchService);

        // Create test server
        _testServer = await IdentityTestServer.CreateAsync(_cluster, _postgresContainer.GetConnectionString(), _searchService);

        // Install and start Playwright
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}");
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !IsDebugMode(),
            SlowMo = IsDebugMode() ? 100 : 0
        });
    }

    public async Task<IBrowserContext> CreateBrowserContextAsync()
    {
        return await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = BaseAddress
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.DisposeAsync();
        }
        _playwright?.Dispose();

        if (_testServer != null)
        {
            await _testServer.DisposeAsync();
        }

        if (_cluster != null)
        {
            await _cluster.StopAllSilosAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    private static bool IsDebugMode()
    {
        return Environment.GetEnvironmentVariable("PLAYWRIGHT_DEBUG") == "1";
    }

    private class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
        }
    }
}

[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "IntegrationTests";
}

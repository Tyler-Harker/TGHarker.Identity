using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.TestingHost;
using TGharker.Identity.Web.Endpoints;
using TGharker.Identity.Web.Middleware;
using TGharker.Identity.Web.Services;

namespace TGHarker.Identity.IntegrationTests.Infrastructure;

public class IdentityTestServer : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly int _port;

    public string BaseAddress => $"http://localhost:{_port}";

    private IdentityTestServer(IHost host, int port)
    {
        _host = host;
        _port = port;
    }

    public static async Task<IdentityTestServer> CreateAsync(TestCluster cluster, string connectionString, TestGrainSearchService searchService)
    {
        // Find an available port
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        // Find the web project directory for content root
        var webProjectDir = FindWebProjectDirectory();

        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseKestrel(options =>
                {
                    options.ListenLocalhost(port);
                });
                webBuilder.UseEnvironment("Testing");
                webBuilder.UseContentRoot(webProjectDir);

                webBuilder.ConfigureServices(services =>
                {
                    // Add Orleans client from test cluster
                    services.AddSingleton<IClusterClient>(cluster.Client);

                    // Configure forwarded headers for reverse proxy
                    services.Configure<ForwardedHeadersOptions>(options =>
                    {
                        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                        options.KnownIPNetworks.Clear();
                        options.KnownProxies.Clear();
                    });

                    // Use test search service instead of Orleans Search
                    services.AddSingleton<IGrainSearchService>(searchService);

                    // Add services to the container - include the Web project assembly for Razor Pages
                    var webAssembly = typeof(TGharker.Identity.Web.Pages.Tenant.LoginModel).Assembly;
                    services.AddRazorPages()
                        .AddApplicationPart(webAssembly);

                    // Register identity services
                    services.AddSingleton<IPasswordHasher, PasswordHasher>();
                    services.AddSingleton<IPkceValidator, PkceValidator>();
                    services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
                    services.AddScoped<IClientAuthenticationService, ClientAuthenticationService>();
                    services.AddScoped<ITenantResolver, TenantResolver>();
                    services.AddScoped<ISessionService, SessionService>();
                    services.AddScoped<IUserFlowService, UserFlowService>();
                    services.AddScoped<IOrganizationCreationService, OrganizationCreationService>();
                    services.AddSingleton<IOAuthCorsService, OAuthCorsService>();
                    services.AddScoped<IAdminService, AdminService>();
                    services.AddSingleton<ITenantSigningKeyResolver, TenantSigningKeyResolver>();
                    services.AddMemoryCache();

                    // OAuth utilities
                    services.AddSingleton<IOAuthTokenGenerator, OAuthTokenGenerator>();
                    services.AddSingleton<IOAuthUrlBuilder, OAuthUrlBuilder>();
                    services.AddSingleton<IOAuthParameterParser, OAuthParameterParser>();
                    services.AddSingleton<IOAuthResponseBuilder, OAuthResponseBuilder>();

                    // Data protection for testing
                    services.AddDataProtection()
                        .SetApplicationName("TGHarker.Identity");

                    // Authentication
                    services.AddAuthentication(options =>
                        {
                            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        })
                        .AddCookie(options =>
                        {
                            options.LoginPath = "/Account/Login";
                            options.LogoutPath = "/Account/Logout";
                            options.Cookie.Name = "TGHarker.Identity.Session";
                            options.Cookie.HttpOnly = true;
                            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
                            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                            options.SlidingExpiration = true;
                        })
                        .AddCookie("SuperAdmin", options =>
                        {
                            options.LoginPath = "/Admin/Login";
                            options.LogoutPath = "/Admin/Logout";
                            options.Cookie.Name = "TGHarker.Identity.SuperAdmin";
                            options.Cookie.HttpOnly = true;
                            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
                            options.ExpireTimeSpan = TimeSpan.FromHours(2);
                        });

                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("SuperAdmin", policy =>
                            policy.AddAuthenticationSchemes("SuperAdmin")
                                  .RequireClaim("is_superadmin", "true"));
                    });

                    services.AddEndpointsApiExplorer();
                });

                webBuilder.Configure((context, app) =>
                {
                    app.UseForwardedHeaders();

                    app.UseRouting();

                    // Handle CORS for OAuth2/OIDC endpoints
                    app.UseOAuthCors();

                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseSessionValidation();
                    app.UseTenantRequired();

                    app.UseEndpoints(endpoints =>
                    {
                        // Map OAuth2/OIDC endpoints
                        endpoints.MapDiscoveryEndpoints();
                        endpoints.MapAuthorizeEndpoint();
                        endpoints.MapTokenEndpoint();
                        endpoints.MapUserInfoEndpoint();
                        endpoints.MapRevocationEndpoint();
                        endpoints.MapIntrospectionEndpoint();
                        endpoints.MapTenantEndpoints();
                        endpoints.MapStatsEndpoint();
                        endpoints.MapThemeEndpoint();

                        // Map Razor Pages
                        endpoints.MapRazorPages();
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();

        return new IdentityTestServer(host, port);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private static string FindWebProjectDirectory()
    {
        // Start from the current directory and walk up to find the solution
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TGHarker.Identity.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new InvalidOperationException($"Could not find solution directory starting from {Directory.GetCurrentDirectory()}");
        }

        var webProjectDir = Path.Combine(dir.FullName, "TGharker.Identity.Web");
        if (!Directory.Exists(webProjectDir))
        {
            throw new InvalidOperationException($"Could not find web project directory at {webProjectDir}");
        }

        return webProjectDir;
    }
}

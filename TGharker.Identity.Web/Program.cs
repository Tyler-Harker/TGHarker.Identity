using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Orleans.Configuration;
using TGharker.Identity.Web.Endpoints;
using TGharker.Identity.Web.Middleware;
using TGharker.Identity.Web.Services;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Configure forwarded headers for reverse proxy (SSL termination)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Clear the default known networks/proxies to allow forwarded headers from any source
    // In production, you may want to restrict this to specific proxy IPs
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Check if running in test mode (test fixture will inject IClusterClient)
var isTestMode = builder.Environment.EnvironmentName == "Testing";

// Check if running under Aspire (Aspire sets Orleans clustering via configuration)
var isAspireManaged = !string.IsNullOrEmpty(builder.Configuration["Orleans:Clustering:ProviderType"]);

if (!isTestMode)
{
    if (isAspireManaged)
    {
        // Orleans client - Aspire auto-configures clustering from WithReference(orleans.AsClient())
        builder.AddKeyedAzureTableServiceClient("clustering");
        builder.UseOrleansClient();
    }
    else
    {
        // Production: Configure Orleans client manually
        var storageConnectionString = builder.Configuration.GetConnectionString("AzureStorage")
            ?? throw new InvalidOperationException("Azure Storage connection string not configured. Set 'ConnectionStrings:AzureStorage'.");

        builder.UseOrleansClient(clientBuilder =>
        {
            clientBuilder.Configure<ClusterOptions>(options =>
            {
                options.ClusterId = builder.Configuration["Orleans:ClusterId"] ?? "identity-cluster";
                options.ServiceId = builder.Configuration["Orleans:ServiceId"] ?? "identity-service";
            });

            clientBuilder.UseAzureStorageClustering(options =>
            {
                options.TableServiceClient = new TableServiceClient(storageConnectionString);
            });
        });
    }
}

// Add Orleans Search with PostgreSQL for querying grains
// In test mode, the test fixture will provide the DbContext
if (!isTestMode)
{
    builder.AddNpgsqlDbContext<PostgreSqlSearchContext>("searchdb-identity");
}
builder.Services.AddOrleansSearch();

// Add services to the container
builder.Services.AddRazorPages();

// Register identity services
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IPkceValidator, PkceValidator>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IClientAuthenticationService, ClientAuthenticationService>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IGrainSearchService, GrainSearchService>();
builder.Services.AddScoped<IUserFlowService, UserFlowService>();
builder.Services.AddScoped<IOrganizationCreationService, OrganizationCreationService>();
builder.Services.AddSingleton<IOAuthCorsService, OAuthCorsService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddSingleton<ITenantSigningKeyResolver, TenantSigningKeyResolver>();
builder.Services.AddMemoryCache();

// Configure JWT bearer options with dynamic tenant-based signing key resolution
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IClusterClient>((options, clusterClient) =>
    {
        // Disable claim type remapping so "sub" stays as "sub" (not remapped to NameIdentifier)
        // and "tenant_id" stays as "tenant_id"
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false, // We validate issuer host in OnTokenValidated event
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // Dynamically resolve signing keys based on the token's issuer (which contains tenant info)
            IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
            {
                // Extract tenant identifier from issuer (format: https://host/tenant/{tenantId})
                var issuer = securityToken.Issuer;
                var tenantId = ExtractTenantIdFromIssuer(issuer);

                if (string.IsNullOrEmpty(tenantId))
                {
                    return [];
                }

                // Get signing keys from the tenant's grain
                // Note: This uses blocking wait because IssuerSigningKeyResolver is synchronous
                var signingKeyGrain = clusterClient.GetGrain<TGHarker.Identity.Abstractions.Grains.ISigningKeyGrain>($"{tenantId}/signing-keys");
                var publicKeys = signingKeyGrain.GetPublicKeysAsync().GetAwaiter().GetResult();

                var securityKeys = new List<Microsoft.IdentityModel.Tokens.SecurityKey>();
                foreach (var key in publicKeys)
                {
                    try
                    {
                        var rsa = System.Security.Cryptography.RSA.Create();
                        rsa.ImportFromPem(key.PublicKeyPem);
                        securityKeys.Add(new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa) { KeyId = key.KeyId });
                    }
                    catch
                    {
                        // Skip invalid keys
                    }
                }

                return securityKeys;
            }
        };

        // Validate that the token's issuer host matches the current request host
        // This prevents accepting tokens from other identity servers
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var issuer = context.Principal?.FindFirst("iss")?.Value;
                if (string.IsNullOrEmpty(issuer))
                {
                    context.Fail("Token is missing issuer claim");
                    return Task.CompletedTask;
                }

                // Parse the issuer URI and validate the host matches the current request
                if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
                {
                    context.Fail("Invalid issuer format");
                    return Task.CompletedTask;
                }

                var requestHost = context.HttpContext.Request.Host.Host;
                var issuerHost = issuerUri.Host;

                // Compare hosts (case-insensitive)
                if (!string.Equals(requestHost, issuerHost, StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail($"Issuer host '{issuerHost}' does not match request host '{requestHost}'");
                    return Task.CompletedTask;
                }

                return Task.CompletedTask;
            }
        };
    });

static string? ExtractTenantIdFromIssuer(string? issuer)
{
    if (string.IsNullOrEmpty(issuer))
        return null;

    // Issuer format: https://host/tenant/{tenantId} or https://host/tenant/{tenantId}/
    const string tenantSegment = "/tenant/";
    var index = issuer.IndexOf(tenantSegment, StringComparison.OrdinalIgnoreCase);
    if (index < 0)
        return null;

    var startIndex = index + tenantSegment.Length;
    var endIndex = issuer.IndexOf('/', startIndex);

    return endIndex > 0
        ? issuer[startIndex..endIndex]
        : issuer[startIndex..];
}

// Configure Data Protection to persist keys across restarts
var dataProtectionConnectionString = builder.Configuration.GetConnectionString("AzureStorage");
if (!string.IsNullOrEmpty(dataProtectionConnectionString))
{
    var blobServiceClient = new BlobServiceClient(dataProtectionConnectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient("dataprotection-keys");
    containerClient.CreateIfNotExists();
    var blobClient = containerClient.GetBlobClient("keys.xml");

    builder.Services.AddDataProtection()
        .SetApplicationName("TGHarker.Identity")
        .PersistKeysToAzureBlobStorage(blobClient);
}
else
{
    // Development fallback - keys stored in memory (will be lost on restart)
    builder.Services.AddDataProtection()
        .SetApplicationName("TGHarker.Identity");
}

// Authentication - support both cookies (for Razor Pages), JWT Bearer (for API), and SuperAdmin
builder.Services.AddAuthentication(options =>
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme)
    .AddCookie("SuperAdmin", options =>
    {
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Admin/Logout";
        options.Cookie.Name = "TGHarker.Identity.SuperAdmin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdmin", policy =>
        policy.AddAuthenticationSchemes("SuperAdmin")
              .RequireClaim("is_superadmin", "true"));
});

// Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
// Must be first to correctly set scheme/host from reverse proxy headers
app.UseForwardedHeaders();

// Diagnostic logging for OAuth responses (helps debug content-type issues)
app.UseResponseLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Use JSON error responses for OAuth/API endpoints, HTML for Razor Pages
    app.UseExceptionHandler(exceptionApp =>
    {
        exceptionApp.Run(async context =>
        {
            var path = context.Request.Path.Value ?? "/";

            // Return JSON for OAuth/API endpoints
            if (path.Contains("/connect/", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("/api/", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"server_error\",\"error_description\":\"An unexpected error occurred\"}");
            }
            else
            {
                // Redirect to Error page for browser requests
                context.Response.Redirect("/Error");
            }
        });
    });
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Handle CORS for OAuth2/OIDC endpoints
app.UseOAuthCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseSessionValidation();
app.UseTenantRequired();

// Map OAuth2/OIDC endpoints
app.MapDiscoveryEndpoints();
app.MapAuthorizeEndpoint();
app.MapTokenEndpoint();
app.MapUserInfoEndpoint();
app.MapRevocationEndpoint();
app.MapIntrospectionEndpoint();
app.MapTenantEndpoints();
app.MapStatsEndpoint();
app.MapThemeEndpoint();

// Map Razor Pages
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

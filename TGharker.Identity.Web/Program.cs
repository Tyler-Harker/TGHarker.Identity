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

// Check if running under Aspire (Aspire sets Orleans clustering via configuration)
var isAspireManaged = !string.IsNullOrEmpty(builder.Configuration["Orleans:Clustering:ProviderType"]);

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

// Add Orleans Search with PostgreSQL for querying grains
builder.AddNpgsqlDbContext<PostgreSqlSearchContext>("searchdb-identity");
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
builder.Services.AddMemoryCache();

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
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false, // Will be validated per-tenant
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false, // Will be validated per-tenant
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    })
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
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

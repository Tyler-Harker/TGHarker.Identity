using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Orleans.Configuration;
using TGharker.Identity.Web.Endpoints;
using TGharker.Identity.Web.Middleware;
using TGharker.Identity.Web.Services;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

// Authentication - support both cookies (for Razor Pages) and JWT Bearer (for API)
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
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
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
    });

builder.Services.AddAuthorization();

// Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
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

app.UseAuthentication();
app.UseAuthorization();
app.UseTenantRequired();

// Map OAuth2/OIDC endpoints
app.MapDiscoveryEndpoints();
app.MapAuthorizeEndpoint();
app.MapTokenEndpoint();
app.MapUserInfoEndpoint();
app.MapRevocationEndpoint();
app.MapIntrospectionEndpoint();
app.MapTenantEndpoints();

// Map Razor Pages
app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using TGHarker.Identity.Client;
using TGHarker.Identity.ExampleWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Orleans client - Aspire auto-configures clustering from WithReference(orleans.AsClient())
builder.AddKeyedAzureTableServiceClient("clustering");
builder.UseOrleansClient();

// Register client secret store (shares secret between DataSeedingService and PermissionSyncService)
builder.Services.AddSingleton<ClientSecretStore>();

// Register data seeding service (seeds user, tenant, client via Orleans)
builder.Services.AddHostedService<DataSeedingService>();

// Note: PermissionSyncService is no longer needed since we're using SyncOnStartup = true
// with a hardcoded secret in the IdentityClient configuration

// Add Razor Pages
builder.Services.AddRazorPages();

// Configure authentication
var identityUrl = builder.Configuration["Identity:Authority"] ?? "https://localhost:7157";
var tenantId = builder.Configuration["Identity:TenantId"] ?? "test";

// Configure TGHarker.Identity.Client for application permissions/roles
// ⚠️ TESTING ONLY: Using hardcoded secret for deterministic example/testing scenarios
// Permissions and roles defined here are synced to the identity server on startup.
builder.Services.AddTGHarkerIdentityClient(
    options =>
    {
        options.Authority = identityUrl;
        options.ClientId = "testweb";
        options.ClientSecret = "Hardcoded testing secret"; // ⚠️ HARDCODED FOR TESTING ONLY
        options.TenantId = tenantId;
        options.SyncOnStartup = true; // Sync permissions/roles on startup
    },
    permissions =>
    {
        // Define application permissions - these are synced via SDK on startup
        permissions
            .AddPermission("dashboard:view", "View Dashboard", "Access the main dashboard")
            .AddPermission("reports:read", "Read Reports", "View reports and analytics")
            .AddPermission("reports:export", "Export Reports", "Export reports to PDF/CSV")
            .AddPermission("settings:read", "View Settings", "View application settings")
            .AddPermission("settings:write", "Modify Settings", "Change application settings")
            .AddPermission("users:read", "View Users", "View user list and profiles")
            .AddPermission("users:invite", "Invite Users", "Invite new users to the application");

        // Define application roles with full metadata
        permissions
            .AddRole(new Role("viewer")
            {
                Id = "role-viewer",
                DisplayName = "Viewer",
                Description = "Can view dashboard and reports",
                Permissions = ["dashboard:view", "reports:read"]
            })
            .AddRole(new Role("analyst")
            {
                Id = "role-analyst",
                DisplayName = "Analyst",
                Description = "Can view and export reports",
                Permissions = ["dashboard:view", "reports:read", "reports:export"]
            })
            .AddRole(new Role("admin")
            {
                Id = "role-admin",
                DisplayName = "Administrator",
                Description = "Full access to all features",
                Permissions = ["dashboard:view", "reports:read", "reports:export",
                               "settings:read", "settings:write", "users:read", "users:invite"]
            });
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "ExampleWeb.Session";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
})
.AddOpenIdConnect(options =>
{
    options.Authority = $"{identityUrl}/tenant/{tenantId}";
    options.ClientId = "testweb";
    options.ClientSecret = "Hardcoded testing secret";
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = false;
    options.UsePkce = true;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");

    options.CallbackPath = "/signin-oidc";
    options.SignedOutCallbackPath = "/signout-callback-oidc";
    options.SignedOutRedirectUri = "/";

    // Development: disable HTTPS metadata requirement and certificate validation
    if (builder.Environment.IsDevelopment())
    {
        options.RequireHttpsMetadata = false;
        options.BackchannelHttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    }

    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        NameClaimType = "name",
        RoleClaimType = "role"
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

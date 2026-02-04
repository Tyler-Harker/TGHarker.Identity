using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using TGHarker.Identity.Client;
using TGHarker.Identity.ExampleOrganizationWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Orleans client - Aspire auto-configures clustering from WithReference(orleans.AsClient())
builder.AddKeyedAzureTableServiceClient("clustering");
builder.UseOrleansClient();

// Register client secret store (shares secret between DataSeedingService and PermissionSyncService)
builder.Services.AddSingleton<ClientSecretStore>();

// Register data seeding service (seeds client via Orleans)
builder.Services.AddHostedService<DataSeedingService>();

// Register permission sync service (syncs permissions/roles via SDK after DataSeedingService sets the secret)
builder.Services.AddHostedService<PermissionSyncService>();

// Add Razor Pages
builder.Services.AddRazorPages();

// Configure authentication
var identityUrl = builder.Configuration["Identity:Authority"] ?? "https://localhost:7157";
var tenantId = builder.Configuration["Identity:TenantId"] ?? "test";

// Configure TGHarker.Identity.Client for application permissions/roles
// Note: SyncOnStartup is disabled because PermissionSyncService handles sync manually
// after DataSeedingService creates the client and provides the secret.
// Permissions and roles defined here are synced to the identity server on startup.
builder.Services.AddTGHarkerIdentityClient(
    options =>
    {
        options.Authority = identityUrl;
        options.ClientId = "testorgweb";
        options.ClientSecret = "unused"; // Not used - PermissionSyncService gets secret from ClientSecretStore
        options.TenantId = tenantId;
        options.SyncOnStartup = false; // Disabled - PermissionSyncService handles sync
    },
    permissions =>
    {
        // Organization-focused permissions for team collaboration
        permissions
            .AddPermission("org:view", "View Organization", "View organization details")
            .AddPermission("org:manage", "Manage Organization", "Update organization settings")
            .AddPermission("members:view", "View Members", "View organization members")
            .AddPermission("members:invite", "Invite Members", "Invite new members to the organization")
            .AddPermission("members:remove", "Remove Members", "Remove members from the organization")
            .AddPermission("projects:read", "Read Projects", "View projects in the organization")
            .AddPermission("projects:write", "Write Projects", "Create and edit projects")
            .AddPermission("projects:delete", "Delete Projects", "Delete projects")
            .AddPermission("billing:view", "View Billing", "View billing information")
            .AddPermission("billing:manage", "Manage Billing", "Update payment methods and subscriptions");

        // Define organization roles with full metadata
        permissions
            .AddRole(new Role("member")
            {
                Id = "role-member",
                DisplayName = "Member",
                Description = "Basic organization member with read access",
                Permissions = ["org:view", "members:view", "projects:read"]
            })
            .AddRole(new Role("contributor")
            {
                Id = "role-contributor",
                DisplayName = "Contributor",
                Description = "Can create and edit projects",
                Permissions = ["org:view", "members:view", "projects:read", "projects:write"]
            })
            .AddRole(new Role("manager")
            {
                Id = "role-manager",
                DisplayName = "Manager",
                Description = "Can manage members and projects",
                Permissions = ["org:view", "members:view", "members:invite", "projects:read", "projects:write", "projects:delete"]
            })
            .AddRole(new Role("owner")
            {
                Id = "role-owner",
                DisplayName = "Owner",
                Description = "Full access to organization including billing",
                Permissions = ["org:view", "org:manage", "members:view", "members:invite", "members:remove",
                               "projects:read", "projects:write", "projects:delete", "billing:view", "billing:manage"]
            });
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "ExampleOrgWeb.Session";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
})
.AddOpenIdConnect(options =>
{
    options.Authority = $"{identityUrl}/tenant/{tenantId}";
    options.ClientId = "testorgweb";
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

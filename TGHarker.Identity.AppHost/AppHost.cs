using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("env");
// Azure Storage for Orleans persistence (using emulator in development)
var storage = builder.AddAzureStorage("storage");


var clusteringTable = storage.AddTables("clustering");
var grainStorage = storage.AddBlobs("grainstate");

// PostgreSQL for search index
var postgres = builder.AddAzurePostgresFlexibleServer("postgres");
var searchDb = postgres.AddDatabase("searchdb-identity");

// Orleans cluster - use "Default-inner" so silo can wrap it with searchable storage as "Default"
var orleans = builder.AddOrleans("identity-cluster")
    .WithClustering(clusteringTable)
    .WithGrainStorage("Default-inner", grainStorage);

// Identity Silo (Orleans grain host)
var silo = builder.AddProject<Projects.TGHarker_Identity_Silo>("identity-silo")
    .WithReference(orleans)
    .WithReference(searchDb)
    .WaitFor(storage)
    .WaitFor(searchDb);

// Custom domain parameters (using underscores to avoid Bicep generation issues with hyphens)
var customDomain = builder.AddParameter("customDomain", value: "identity.harker.dev", publishValueAsDefault: true);
var certificateName = builder.AddParameter("certificateName", value: "identity.harker.dev-envnizco-260128040623", publishValueAsDefault: true);

// SuperAdmin credentials (secret in production, default for dev)
var devSuperAdminPassword = "admin";
var superAdminUsername = builder.AddParameter("superAdminUsername", value: "admin", publishValueAsDefault: false);
var superAdminPassword = builder.Environment.IsDevelopment()
    ? builder.AddParameter("superAdminPassword", value: devSuperAdminPassword, publishValueAsDefault: false)
    : builder.AddParameter("superAdminPassword", secret: true);

// Identity Web (Razor Pages UI + OAuth2/OIDC API + Orleans client)
var identityWeb = builder.AddProject<Projects.TGharker_Identity_Web>("identity-web")
    .WithReference(orleans.AsClient())
    .WithReference(searchDb)
    .WaitFor(silo)
    .WithExternalHttpEndpoints()
    .WithEnvironment("SUPERADMIN_USERNAME", superAdminUsername)
    .WithEnvironment("SUPERADMIN_PASSWORD", superAdminPassword);

// Example Web (Demo client application with data seeding)
var exampleWeb = builder.AddProject<Projects.TGHarker_Identity_ExampleWeb>("example-web")
    .WithReference(orleans.AsClient())
    .WaitFor(silo)
    .WaitFor(identityWeb)
    .WithExternalHttpEndpoints();

// Example Organization Web (Demo client with organization prompt mode)
var exampleOrgWeb = builder.AddProject<Projects.TGHarker_Identity_ExampleOrganizationWeb>("example-org-web")
    .WithReference(orleans.AsClient())
    .WaitFor(silo)
    .WaitFor(identityWeb)
    .WithExternalHttpEndpoints();

if (!builder.Environment.IsDevelopment())
{
    identityWeb.PublishAsAzureContainerApp((infra, app) =>
    {
#pragma warning disable ASPIREACADOMAINS001
        app.ConfigureCustomDomain(customDomain, certificateName);
#pragma warning restore ASPIREACADOMAINS001
    });
}


if (builder.Environment.IsDevelopment())
{
    storage.RunAsEmulator();
    var devPostgrePasswordValue = "eWK3tuA5SxUWJqJ3uTXsU7";
    var devPostgreUsername = builder.AddParameter("devPostgreUsername", "postgres");
    var devPostgrePassword = builder.AddParameter("devPostgrePassword", devPostgrePasswordValue);
    postgres.RunAsContainer(x => x.WithUserName(devPostgreUsername).WithPassword(devPostgrePassword));

    // Add pgAdmin for database management
    var pgadminConfigPath = Path.Combine(builder.AppHostDirectory, "pgadmin");
    builder.AddContainer("pgadmin", "dpage/pgadmin4")
        .WithEnvironment("PGADMIN_DEFAULT_EMAIL", "admin@admin.com")
        .WithEnvironment("PGADMIN_DEFAULT_PASSWORD", devPostgrePasswordValue)
        .WithEnvironment("PGADMIN_CONFIG_SERVER_MODE", "False")
        .WithEnvironment("PGADMIN_CONFIG_MASTER_PASSWORD_REQUIRED", "False")
        .WithBindMount(Path.Combine(pgadminConfigPath, "servers.json"), "/pgadmin4/servers.json", isReadOnly: true)
        .WithHttpEndpoint(port: 5050, targetPort: 80)
        .WaitFor(postgres);
}

builder.Build().Run();

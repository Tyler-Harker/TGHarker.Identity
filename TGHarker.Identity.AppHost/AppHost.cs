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

// Identity Web (Razor Pages UI + OAuth2/OIDC API + Orleans client)
var identityWeb = builder.AddProject<Projects.TGharker_Identity_Web>("identity-web")
    .WithReference(orleans.AsClient())
    .WithReference(searchDb)
    .WaitFor(silo)
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
    postgres.RunAsContainer();
}

builder.Build().Run();

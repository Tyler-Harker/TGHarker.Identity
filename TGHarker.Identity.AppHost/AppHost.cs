var builder = DistributedApplication.CreateBuilder(args);

// Azure Storage for Orleans persistence (using emulator in development)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var clusteringTable = storage.AddTables("clustering");
var grainStorage = storage.AddBlobs("grainstate");

// Azure Storage Explorer UI (web-based) - access via Aspire dashboard
builder.AddContainer("storage-explorer", "sebagomez/azurestorageexplorer")
    .WithHttpEndpoint(targetPort: 8080)
    .WithEnvironment("AZURE_STORAGE_CONNECTIONSTRING", grainStorage)
    .WaitFor(storage);

// PostgreSQL for search index
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();
var searchDb = postgres.AddDatabase("searchdb");

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

// Identity Web (Razor Pages UI + OAuth2/OIDC API + Orleans client)
builder.AddProject<Projects.TGharker_Identity_Web>("identity-web")
    .WithReference(orleans.AsClient())
    .WithReference(searchDb)
    .WaitFor(silo)
    .WithExternalHttpEndpoints();

builder.Build().Run();

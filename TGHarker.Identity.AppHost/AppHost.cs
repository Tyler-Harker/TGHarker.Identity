var builder = DistributedApplication.CreateBuilder(args);

// Azure Storage for Orleans persistence (using emulator in development)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobStorage = storage.AddBlobs("blobs");
var tableStorage = storage.AddTables("tables");

// Orleans cluster
var orleans = builder.AddOrleans("identity-cluster")
    .WithClustering(tableStorage)
    .WithGrainStorage("Default", blobStorage);

// Identity Silo (Orleans grain host)
var silo = builder.AddProject<Projects.TGHarker_Identity_Silo>("identity-silo")
    .WithReference(orleans)
    .WithReference(blobStorage)
    .WithReference(tableStorage);

// Identity Web (Razor Pages UI + OAuth2/OIDC API + Orleans client)
var web = builder.AddProject<Projects.TGharker_Identity_Web>("identity-web")
    .WithReference(orleans)
    .WaitFor(silo)
    .WithExternalHttpEndpoints();

builder.Build().Run();

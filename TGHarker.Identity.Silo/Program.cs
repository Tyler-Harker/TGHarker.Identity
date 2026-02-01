using System.Net;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Orleans.Configuration;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.Orleans.Extensions;
using TGHarker.Orleans.Search.PostgreSQL.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddKeyedAzureTableServiceClient("clustering");
builder.AddKeyedAzureBlobServiceClient("grainstate");

// Add Orleans Search with PostgreSQL (Aspire provides the connection)


// Check if running under Aspire (Aspire sets Orleans clustering via configuration)
var isAspireManaged = !string.IsNullOrEmpty(builder.Configuration["Orleans:Clustering:ProviderType"]);

// Check if running in local development (not in Azure Container Apps)
var isLocalDevelopment = builder.Environment.IsDevelopment() ||
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTAINER_APP_NAME"));

var connectionString = builder.Configuration.GetConnectionString("searchdb-identity")
    ?? "Host=localhost;Database=searchdb;Username=postgres;Password=postgres";

// Orleans silo configuration - Aspire auto-configures clustering and grain storage
builder.UseOrleans(siloBuilder =>
{
    if (isAspireManaged)
    {
        // Only use loopback in local development (not in ACA)
        if (isLocalDevelopment)
        {
            siloBuilder.Configure<EndpointOptions>(options =>
            {
                options.AdvertisedIPAddress = IPAddress.Loopback;
            });
        }

        // Configure searchable grain storage as "Default" (what grains use)
        // wrapping Azure Blob storage as the inner persistence layer
        siloBuilder.AddSearchableGrainStorageAsDefault((b, innerName) =>
        {
            siloBuilder.AddAspireAzureBlobGrainStorage(innerName, "grainstate");
        });
    }
    else
    {
        // Production: Configure Orleans clustering and persistence manually
        var storageConnectionString = builder.Configuration.GetConnectionString("AzureStorage")
            ?? throw new InvalidOperationException("Azure Storage connection string not configured. Set 'ConnectionStrings:AzureStorage'.");

        siloBuilder.Configure<ClusterOptions>(options =>
        {
            options.ClusterId = builder.Configuration["Orleans:ClusterId"] ?? "emails-cluster";
            options.ServiceId = builder.Configuration["Orleans:ServiceId"] ?? "emails-service";
        });

        // Use Azure Table Storage for clustering
        siloBuilder.UseAzureStorageClustering(options =>
        {
            options.TableServiceClient = new TableServiceClient(storageConnectionString);
        });

        

        siloBuilder.AddSearchableGrainStorageAsDefault((b, innerName) =>
        {
            siloBuilder.AddAzureBlobGrainStorage(innerName, options =>
            {
                options.BlobServiceClient = new BlobServiceClient(storageConnectionString);
                options.ContainerName = "identity-grains";
            });
        });
    }
});

builder.Services.AddOrleansSearch()
    .UsePostgreSql<SearchDesignTimeContext>(connectionString);

var app = builder.Build();

app.MapDefaultEndpoints();


if(isLocalDevelopment)
{
    // Ensure search database schema is created
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SearchDesignTimeContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
else
{
    // Ensure search database schema is created
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<SearchDesignTimeContext>();
        await dbContext.Database.MigrateAsync();
    }
}


app.Run();

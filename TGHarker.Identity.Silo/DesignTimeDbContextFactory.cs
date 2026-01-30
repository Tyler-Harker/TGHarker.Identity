using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TGHarker.Identity.Abstractions.Models.Generated;
using TGHarker.Orleans.Search.PostgreSQL;

namespace TGHarker.Identity.Silo;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlSearchContext>
{
    public PostgreSqlSearchContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlSearchContext>();

        // Use a placeholder connection string for design-time operations
        // The actual connection string is configured at runtime
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? "Host=localhost;Database=searchdb;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsAssembly("TGHarker.Identity.Silo"));

        // Use the generated SearchDesignTimeContext which explicitly registers all entities
        return new SearchDesignTimeContext(optionsBuilder.Options);
    }
}

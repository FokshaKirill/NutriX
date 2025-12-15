using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure;

public class DatabaseContextFactory : IDesignTimeDbContextFactory<DbContext>
{
    public DbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        // Путь к appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true) // true, чтобы не падало если файла нет
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? "Host=localhost;Port=5432;Database=LifePlannerDb;Username=postgres;Password=12345";

        var optionsBuilder = new DbContextOptionsBuilder<DbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new DbContext(optionsBuilder.Options);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Lidstroem.Infrastructure.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var basePath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "../WebAPI"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not found in appsettings.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}

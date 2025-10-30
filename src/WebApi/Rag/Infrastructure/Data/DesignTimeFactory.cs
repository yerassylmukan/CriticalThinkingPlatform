using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebApi.Rag.Infrastructure.Data;

public class DesignTimeFactory : IDesignTimeDbContextFactory<RagDbContext>
{
    public RagDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("src/WebApi/appsettings.json", true)
            .AddEnvironmentVariables()
            .Build();

        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                   ?? cfg.GetConnectionString("Default")
                   ?? "Host=localhost;Port=5432;Database=ragdb;Username=rag;Password=ragpass";

        var options = new DbContextOptionsBuilder<RagDbContext>()
            .UseNpgsql(conn, npg => npg.UseVector())
            .Options;

        return new RagDbContext(options);
    }
}
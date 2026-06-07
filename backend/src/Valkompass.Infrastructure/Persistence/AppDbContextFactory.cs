using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Valkompass.Infrastructure.Persistence;

/// <summary>
/// Design-time-fabrik så att <c>dotnet ef</c> kan skapa/uppdatera migrations utan att
/// starta hela API:t. Använder samma Npgsql + snake_case-konfiguration som runtime.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=valkompass;Username=valkompass;Password=valkompass";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}

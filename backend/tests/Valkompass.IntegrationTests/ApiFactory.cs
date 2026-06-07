using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Valkompass.IntegrationTests;

/// <summary>
/// Startar API:t mot en slängbar Postgres-container. Miljön sätts till Development så att
/// migrations, innehållsseed och bootstrap-admin körs automatiskt vid uppstart.
///
/// Anslutningssträngen sätts som process-miljövariabel i InitializeAsync (innan första
/// CreateClient bygger värden), eftersom minimal hosting läser GetConnectionString redan
/// i Program.cs innan ConfigureAppConfiguration hinner appliceras.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public const string AdminEmail = "admin@valkompass.local";
    public const string AdminPassword = "Admin123!";

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _db.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _db.GetConnectionString());
        Environment.SetEnvironmentVariable("AdminUser__Email", AdminEmail);
        Environment.SetEnvironmentVariable("AdminUser__Password", AdminPassword);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}

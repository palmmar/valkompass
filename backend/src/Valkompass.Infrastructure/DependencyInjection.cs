using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Valkompass.Application.Contracts;
using Valkompass.Application.Election;
using Valkompass.Infrastructure.Persistence;
using Valkompass.Infrastructure.Services;

namespace Valkompass.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        services.AddScoped<IQuizService, QuizService>();
        services.AddScoped<IBarometerService, BarometerService>();
        services.AddScoped<IElectionSnapshotStore, ElectionSnapshotStore>();

        return services;
    }

    /// <summary>
    /// Registrerar importen av Valmyndighetens resultat. Själva bakgrundstjänsten startar bara
    /// när <see cref="ElectionImport.Enabled"/> är satt, men importern registreras alltid så
    /// att den går att köra manuellt.
    /// </summary>
    public static IServiceCollection AddElectionImport(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ElectionImport>(configuration.GetSection(ElectionImport.SectionName));
        services.TryAddSingletonTimeProvider();

        services.AddHttpClient();
        services.AddSingleton<ElectionSignatureVerifier>();

        services.AddHttpClient<ElectionResultImporter>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ElectionImport>>().Value;
            client.Timeout = options.RequestTimeout;
            // Valmyndigheten ser vem som pollar dem; en identifierbar agent är god ton.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("valkompass.se (+https://github.com/palmmar/valkompass)");
        });

        services.AddHostedService<ElectionImportBackgroundService>();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}

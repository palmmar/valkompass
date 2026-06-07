using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Valkompass.Application.Contracts;
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

        return services;
    }
}

using Valkompass.Application.Contracts;

namespace Valkompass.Api.Endpoints;

/// <summary>
/// Publika, anonyma read-only-endpoints för valbarometern. Helt åtskilda från quiz-/resultat-
/// endpoints – ingen delad route, ingen delad kod, ingen delningstoken.
/// </summary>
public static class BarometerEndpoints
{
    public static IEndpointRouteBuilder MapBarometerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/barometer").WithTags("Barometer");

        group.MapGet("/polls", async (string? pollster, DateOnly? from, DateOnly? to,
                IBarometerService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetPollsAsync(pollster, from, to, ct)))
            .WithName("GetBarometerPolls")
            .WithSummary("Listar enskilda opinionsmätningar (filtrerbart på ?pollster, ?from, ?to). Nyast först.");

        group.MapGet("/latest", async (IBarometerService svc, CancellationToken ct) =>
            {
                var latest = await svc.GetLatestAsync(ct);
                return latest is null ? Results.NotFound() : Results.Ok(latest);
            })
            .WithName("GetBarometerLatest")
            .WithSummary("Senaste läget: färskaste mätningen med förändring sedan senaste riksdagsval.");

        group.MapGet("/timeseries", async (DateOnly? from, DateOnly? to,
                IBarometerService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetTimeseriesAsync(from, to, ct)))
            .WithName("GetBarometerTimeseries")
            .WithSummary("Tidsserie per parti: mätpunkter, månadssnitt, glidande snitt och valresultat.");

        return app;
    }
}

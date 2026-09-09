using Valkompass.Application.Contracts;

namespace Valkompass.Api.Endpoints;

public static class ElectionEndpoints
{
    public static IEndpointRouteBuilder MapElectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/election").WithTags("Election");

        group.MapGet("/live", async (IElectionLiveService live, HttpContext http, CancellationToken ct) =>
            {
                var response = await live.GetLiveAsync(ct);

                // Kort cache: valnatten pollas endpointen tätt av många klienter samtidigt.
                // Importen mot Valmyndigheten är ändå frikopplad från antalet läsare.
                http.Response.Headers.CacheControl = "public, max-age=5";
                return Results.Ok(response);
            })
            .WithName("GetElectionLive")
            .WithSummary("Valvakans läge: fas, rapporteringsgrad, räknat resultat och officiella mandat.")
            .WithDescription(
                "Ett anrop räcker för hela valvakan. Räknat resultat (results) och prognos "
                + "(forecast) är separata fält och ska aldrig presenteras som samma sak. "
                + "Källa för rösträkningen är Valmyndigheten.");

        return app;
    }
}

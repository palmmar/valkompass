using Microsoft.EntityFrameworkCore;
using Valkompass.Application.Contracts;
using Valkompass.Application.Dtos;
using Valkompass.Infrastructure.Persistence;

namespace Valkompass.Api.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Public");

        group.MapGet("/parties/{code}/logo", async (string code, AppDbContext db, HttpContext http, CancellationToken ct) =>
            {
                var logo = await db.PartyLogos
                    .Where(l => l.Party!.Code.ToLower() == code.ToLower())
                    .Select(l => new { l.Data, l.ContentType, l.UpdatedAt })
                    .FirstOrDefaultAsync(ct);
                if (logo is null) return Results.NotFound();

                var etag = $"\"{logo.UpdatedAt.UtcTicks}\"";
                if (http.Request.Headers.IfNoneMatch == etag)
                    return Results.StatusCode(StatusCodes.Status304NotModified);

                http.Response.Headers.CacheControl = "public, max-age=300";
                http.Response.Headers.ETag = etag;
                return Results.File(logo.Data, logo.ContentType);
            })
            .WithName("GetPartyLogo")
            .WithSummary("Hämtar ett partis logotyp (PNG/WebP), eller 404 om ingen är uppladdad.");

        group.MapGet("/questionnaire", async (int? mode, IQuizService quiz, CancellationToken ct) =>
            {
                if (mode is { } m && !QuizModes.MaxTierByMode.ContainsKey(m))
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["mode"] = ["Ogiltigt läge. Tillåtna värden: 25, 50, 75."],
                    });
                return Results.Ok(await quiz.GetQuestionnaireAsync(mode, ct));
            })
            .WithName("GetQuestionnaire")
            .WithSummary("Hämtar det aktiva frågeformuläret (utan partipositioner). ?mode=25|50|75 styr antalet frågor.");

        group.MapPost("/quiz/results", async (SubmitQuizRequest request, IQuizService quiz, CancellationToken ct) =>
            {
                var outcome = await quiz.SubmitAsync(request, ct);
                return outcome.Ok
                    ? Results.Ok(outcome.Response)
                    : Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["answers"] = [.. outcome.Errors!],
                    });
            })
            .WithName("SubmitQuiz")
            .WithSummary("Skickar in svar, beräknar matchning och returnerar en delningstoken.")
            .RequireRateLimiting("submit");

        group.MapGet("/results/{token}", async (string token, IQuizService quiz, CancellationToken ct) =>
            {
                var doc = await quiz.GetResultAsync(token, ct);
                return doc is null ? Results.NotFound() : Results.Ok(doc);
            })
            .WithName("GetResult")
            .WithSummary("Hämtar ett sparat resultat via delningstoken.");

        return app;
    }
}

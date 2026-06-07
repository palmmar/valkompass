using Valkompass.Application.Contracts;
using Valkompass.Application.Dtos;

namespace Valkompass.Api.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Public");

        group.MapGet("/questionnaire", async (IQuizService quiz, CancellationToken ct) =>
                Results.Ok(await quiz.GetQuestionnaireAsync(ct)))
            .WithName("GetQuestionnaire")
            .WithSummary("Hämtar det aktiva frågeformuläret (utan partipositioner).");

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

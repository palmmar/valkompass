using Valkompass.Application.Dtos;

namespace Valkompass.Application.Contracts;

public interface IQuizService
{
    /// <summary>Hämtar det aktiva frågeformuläret (utan partipositioner).</summary>
    Task<QuestionnaireDto> GetQuestionnaireAsync(CancellationToken ct = default);

    /// <summary>Validerar svar, beräknar matchning, sparar resultatet och returnerar en delningstoken.</summary>
    Task<SubmitOutcome> SubmitAsync(SubmitQuizRequest request, CancellationToken ct = default);

    /// <summary>Hämtar ett tidigare sparat resultat via dess delningstoken, eller null om okänd.</summary>
    Task<ResultDocument?> GetResultAsync(string token, CancellationToken ct = default);
}

/// <summary>Utfall av en inlämning – antingen ett svar med token eller valideringsfel.</summary>
public sealed record SubmitOutcome(SubmitQuizResponse? Response, IReadOnlyList<string>? Errors)
{
    public bool Ok => Response is not null;
    public static SubmitOutcome Success(string token) => new(new SubmitQuizResponse(token), null);
    public static SubmitOutcome Invalid(params string[] errors) => new(null, errors);
}

namespace Valkompass.Application.Dtos;

/// <summary>Ett inskickat svar. <c>Value</c> är 1–4, eller null när frågan hoppats över.</summary>
public sealed record SubmitAnswerDto(int QuestionId, int? Value, bool IsSkipped, bool IsImportant);

/// <summary>
/// Begäran för att skicka in ett quiz och beräkna ett resultat. <c>Simplified</c> = true för det
/// förenklade swajp-testet, vilket ger binär matchning (delvis-positioner räknas som helt).
/// <c>Mode</c> är valt läge (25/50/75) och loggas i den anonyma telemetrin; null = okänt.
/// </summary>
public sealed record SubmitQuizRequest(
    IReadOnlyList<SubmitAnswerDto> Answers, bool Simplified = false, int? Mode = null);

/// <summary>Svar med delningstoken till det sparade resultatet.</summary>
public sealed record SubmitQuizResponse(string ShareToken);

/// <summary>
/// Anonym signal om att en kompass påbörjats (första svaret). Loggas för funnel-statistik.
/// Innehåller bara läge + variant – inga svar, ingen koppling till ett resultat.
/// </summary>
public sealed record StartQuizRequest(int Mode, bool Simplified = false);

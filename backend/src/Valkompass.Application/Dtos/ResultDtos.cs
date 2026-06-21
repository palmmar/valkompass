namespace Valkompass.Application.Dtos;

/// <summary>
/// Fullständigt resultatdokument. Detta serialiseras och fryses i QuizSession.ResultJson vid
/// inlämning, och returneras oförändrat av GET /api/results/{token}. Innehåller all data som
/// behövs för att rendera resultatsidan utan omräkning.
/// </summary>
public sealed record ResultDocument(
    DateTimeOffset CreatedAt,
    IReadOnlyList<ResultPartyRefDto> Parties,
    IReadOnlyList<ResultPartyScoreDto> Overall,
    IReadOnlyList<ResultCategoryDto> Categories,
    IReadOnlyList<ResultQuestionDto> Questions,
    string Disclaimer,
    bool Experimental = false);

/// <summary>Referensdata om ett parti (för visning).</summary>
public sealed record ResultPartyRefDto(
    string Code, string Name, string FullName, string? Color, string? ShortDescription);

/// <summary>Ett partis överensstämmelse. <c>AgreementPct</c> null = inget underlag.</summary>
public sealed record ResultPartyScoreDto(string PartyCode, double? AgreementPct, int ComparedQuestionCount);

/// <summary>Partiernas överensstämmelse inom ett politikområde (rankad).</summary>
public sealed record ResultCategoryDto(string Slug, string Name, IReadOnlyList<ResultPartyScoreDto> Parties);

/// <summary>Ett partis svar, överensstämmelse, motivering och källa på en fråga.</summary>
public sealed record ResultQuestionPartyDto(
    string PartyCode,
    int? PartyValue,
    double? AgreementPct,
    string? Motivation,
    string? SourceCitation,
    string? SourceUrl);

/// <summary>Fråga-för-fråga: användarens svar mot varje parti, med motiveringar och källor.</summary>
public sealed record ResultQuestionDto(
    int QuestionId,
    string ExternalKey,
    string Text,
    string? Explanation,
    string? ExplanationSourceUrl,
    string CategorySlug,
    string CategoryName,
    int? UserValue,
    bool Skipped,
    bool IsImportant,
    IReadOnlyList<ResultQuestionPartyDto> Parties);

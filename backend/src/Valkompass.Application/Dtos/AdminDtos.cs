namespace Valkompass.Application.Dtos;

// --- Kategorier ---
public sealed record AdminCategoryDto(int Id, string Slug, string Name, string? Description, string? Icon, int DisplayOrder);
public sealed record CategoryInput(string Slug, string Name, string? Description, string? Icon, int DisplayOrder);

// --- Partier ---
public sealed record AdminPartyDto(int Id, string Code, string Name, string FullName, string? ShortDescription, string? Color, int DisplayOrder, bool IsActive, bool HasLogo);
public sealed record PartyInput(string Code, string Name, string FullName, string? ShortDescription, string? Color, int DisplayOrder, bool IsActive);

// --- Frågor ---
public sealed record AdminQuestionDto(
    int Id, string ExternalKey, string Text, string? Explanation, string? ExplanationSourceUrl,
    int CategoryId, string CategorySlug, string CategoryName, int DisplayOrder, int Tier, bool IsActive);

public sealed record QuestionInput(
    string ExternalKey, string Text, string? Explanation, string? ExplanationSourceUrl, int CategoryId, int DisplayOrder, int Tier, bool IsActive);

// --- Positioner (parti × fråga) ---
public sealed record AdminPositionDto(
    int PartyId, string PartyCode, string PartyName,
    int? Value, string? Motivation, string? SourceCitation, string? SourceUrl);

public sealed record PositionsMatrixDto(int QuestionId, string QuestionText, IReadOnlyList<AdminPositionDto> Positions);

public sealed record PositionInput(int PartyId, int? Value, string? Motivation, string? SourceCitation, string? SourceUrl);
public sealed record PositionsBulkInput(IReadOnlyList<PositionInput> Positions);

// --- Statistik (genomförda kompasser) ---
public sealed record QuizSessionSummaryDto(Guid Id, DateTimeOffset CompletedAt);
public sealed record QuizStatsDto(
    int Total, int Last24h, int Last7d, IReadOnlyList<QuizSessionSummaryDto> Latest);

// --- Svarsfördelning per fråga (aggregerad) ---
public sealed record QuestionAnswerStatsDto(
    int QuestionId, string CategoryName, string Text,
    int Total, int Answered, int Skipped, int Important,
    int StronglyDisagree, int PartlyDisagree, int PartlyAgree, int StronglyAgree,
    bool Suppressed);
public sealed record AnswerStatsDto(int Sessions, IReadOnlyList<QuestionAnswerStatsDto> Questions);

// --- Partimatchning (bästa match per kompass, aggregerad) ---
public sealed record PartyMatchSliceDto(string PartyCode, int Count);
public sealed record PartyMatchStatsDto(int Sessions, int Tied, IReadOnlyList<PartyMatchSliceDto> Slices);

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
    int CategoryId, string CategorySlug, string CategoryName, int DisplayOrder, bool IsActive);

public sealed record QuestionInput(
    string ExternalKey, string Text, string? Explanation, string? ExplanationSourceUrl, int CategoryId, int DisplayOrder, bool IsActive);

// --- Positioner (parti × fråga) ---
public sealed record AdminPositionDto(
    int PartyId, string PartyCode, string PartyName,
    int? Value, string? Motivation, string? SourceCitation, string? SourceUrl);

public sealed record PositionsMatrixDto(int QuestionId, string QuestionText, IReadOnlyList<AdminPositionDto> Positions);

public sealed record PositionInput(int PartyId, int? Value, string? Motivation, string? SourceCitation, string? SourceUrl);
public sealed record PositionsBulkInput(IReadOnlyList<PositionInput> Positions);

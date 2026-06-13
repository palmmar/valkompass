namespace Valkompass.Application.Dtos;

// Admin-CRUD-DTO:er för valbarometern (bakom Admin/Editor cookie-auth). Manuell backstop
// när det automatiska ingest-flödet (SwedishPolls/SCB) släpar nära valet.

public sealed record AdminPollsterDto(
    int Id, string Code, string DisplayName, string? Method, string? Commissioner, int PollCount);

public sealed record PollsterInput(string Code, string DisplayName, string? Method, string? Commissioner);

public sealed record AdminPollDto(
    int Id, string ExternalKey, string PollsterCode, string PollsterName,
    DateOnly? FieldStart, DateOnly? FieldEnd, DateOnly PublishedAt, int? SampleSize,
    string? SourceUrl, string? SourceCitation, IReadOnlyList<AdminPollResultDto> Results);

/// <summary><c>Value</c> null = under redovisningsgräns (redovisas ej), aldrig 0.</summary>
public sealed record AdminPollResultDto(string PartyCode, double? Value, double? MarginOfError);

public sealed record PollInput(
    string ExternalKey, string PollsterCode,
    DateOnly? FieldStart, DateOnly? FieldEnd, DateOnly PublishedAt, int? SampleSize,
    string? SourceUrl, string? SourceCitation, IReadOnlyList<PollResultInput> Results);

public sealed record PollResultInput(string PartyCode, double? Value, double? MarginOfError);

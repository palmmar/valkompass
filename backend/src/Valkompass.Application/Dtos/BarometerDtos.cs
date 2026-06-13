namespace Valkompass.Application.Dtos;

// DTO:er för valbarometern. Helt åtskilda från ResultDtos – ingen delad typ, ingen delad
// endpoint. Publik, anonym, read-only opinionsdata.

/// <summary>Parti-referens för barometern (återanvänder Party: kod, namn, färg, ordning).</summary>
public sealed record BarometerPartyDto(string Code, string Name, string? Color, int DisplayOrder);

/// <summary>Ett institut (primärkälla för en mätning).</summary>
public sealed record BarometerPollsterDto(string Code, string DisplayName, string? Method, string? Commissioner);

/// <summary>En enskild mätning med full proveniens: institut, fältperiod, n och käll-URL.</summary>
public sealed record BarometerPollDto(
    string ExternalKey,
    string PollsterCode,
    string PollsterName,
    string? Method,
    DateOnly? FieldStart,
    DateOnly? FieldEnd,
    DateOnly PublishedAt,
    int? SampleSize,
    string? SourceUrl,
    string? SourceCitation,
    IReadOnlyList<BarometerPollResultDto> Results);

/// <summary><c>Value</c> null = under institutets redovisningsgräns ("redovisas ej"), aldrig 0.</summary>
public sealed record BarometerPollResultDto(string PartyCode, double? Value, double? MarginOfError);

// --- Tidsserie ---

/// <summary>En enskild mätpunkt (prick) i trenddiagrammet. Endast redovisade värden.</summary>
public sealed record BarometerPointDto(
    DateOnly Date, double Value, double? MarginOfError, string PollsterCode, int? SampleSize, string? SourceUrl);

public sealed record BarometerSeriesDto(string PartyCode, IReadOnlyList<BarometerPointDto> Points);

/// <summary>
/// En punkt i en utjämnad linje (månadssnitt eller glidande snitt). <c>MarginOfError</c> är
/// medelfelmarginalen för de underliggande mätningarna och ger osäkerhetsbandet runt linjen.
/// </summary>
public sealed record BarometerAvgPointDto(DateOnly Date, double Value, double? MarginOfError);

public sealed record BarometerAvgSeriesDto(string PartyCode, IReadOnlyList<BarometerAvgPointDto> Points);

/// <summary>Ett faktiskt valresultat – hålls åtskilt från opinion (visas som egen referens).</summary>
public sealed record BarometerElectionDto(DateOnly Date, string Label, IReadOnlyList<BarometerPartyValueDto> Results);

public sealed record BarometerPartyValueDto(string PartyCode, double? Value);

public sealed record BarometerTimeseriesDto(
    IReadOnlyList<BarometerPartyDto> Parties,
    IReadOnlyList<BarometerSeriesDto> Series,
    IReadOnlyList<BarometerAvgSeriesDto> MonthlyAverage,
    IReadOnlyList<BarometerAvgSeriesDto> RollingAverage,
    int RollingWindowDays,
    IReadOnlyList<BarometerElectionDto> Elections,
    DateOnly? From,
    DateOnly? To,
    string Disclaimer);

// --- Senaste läget ---

/// <summary><c>ElectionDelta</c> = procentenheter mot senaste riksdagsval; null om underlag saknas.</summary>
public sealed record BarometerLatestResultDto(string PartyCode, double? Value, double? MarginOfError, double? ElectionDelta);

public sealed record BarometerLatestDto(
    IReadOnlyList<BarometerPartyDto> Parties,
    BarometerPollDto Poll,
    int? ElectionYear,
    IReadOnlyList<BarometerLatestResultDto> Results,
    string Disclaimer);

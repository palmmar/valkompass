using System.Text.Json.Serialization;
using Valkompass.Application.Election;

namespace Valkompass.Application.Dtos;

// Kontraktet för GET /api/election/live. Frontend ska bara behöva det här ena anropet för
// huvudvyn och ska aldrig känna till Valmyndighetens filstruktur (#85).

/// <summary>Var siffrorna kommer ifrån och hur färska de är.</summary>
public sealed record ElectionSourceDto(
    // Valmyndighetens egen tidsstämpel för när siffrorna senast uppdaterades.
    DateTimeOffset UpdatedAt,
    // När vi hämtade dem.
    DateTimeOffset IngestedAt,
    // Sant när källan inte uppdaterats på ett tag – visa senaste siffror som fördröjda.
    bool Stale,
    // Sant för genrepsdata. Får aldrig presenteras som verkligt valresultat.
    bool IsTest,
    // "preliminary" eller "final".
    string Stage);

/// <summary>
/// Rapporteringsgrad. Distrikt och röstberättigade är olika mått och exponeras separat –
/// andelen räknade valdistrikt är inte "andel av rösterna räknade".
/// </summary>
public sealed record ElectionReportingDto(
    int DistrictsReported,
    int DistrictsTotal,
    int EligibleVotersCovered,
    int EligibleVotersTotal,
    decimal? CoveragePercent,
    int TotalVotes,
    decimal? TurnoutPercent,
    decimal? TurnoutPercentPrevious);

/// <summary>Ett partis faktiskt räknade resultat. Innehåller aldrig prognos.</summary>
public sealed record ElectionPartyResultDto(
    string PartyCode,
    string Name,
    int DisplayOrder,
    int Votes,
    decimal SharePercent,
    decimal? SharePreviousPercent,
    decimal? ShareChangePoints);

/// <summary>Officiell preliminär mandatfördelning från Valmyndigheten – inte vår egen räkning.</summary>
public sealed record ElectionMandateDto(
    string PartyCode,
    int Mandates,
    int FixedMandates,
    int LevellingMandates,
    int? MandatesPrevious,
    int? Change);

public sealed record ElectionThresholdsDto(decimal NationalPercent, decimal ConstituencyPercent);

/// <summary>
/// Hela valvakans läge i ett svar. <see cref="Results"/> är räknat resultat och
/// <see cref="Forecast"/> är prognos – de är avsiktligt skilda fält och blandas aldrig ihop.
/// </summary>
public sealed record ElectionLiveResponse(
    [property: JsonConverter(typeof(CamelCaseJsonStringEnumConverter<ElectionPhase>))]
    ElectionPhase Phase,
    ElectionSourceDto? Source,
    ElectionReportingDto? Reporting,
    IReadOnlyList<ElectionPartyResultDto> Results,
    IReadOnlyList<ElectionMandateDto> OfficialMandates,
    ElectionThresholdsDto Thresholds,
    // Prognos. Alltid null i v1 – nowcasten ligger i #84. Fältet finns med från början så att
    // kontraktet inte behöver ändras när den kommer.
    object? Forecast = null);

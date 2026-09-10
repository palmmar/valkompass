namespace Valkompass.Application.Dtos;

/// <summary>
/// Ett partis prognos. Innehåller medvetet inte det räknade resultatet – det ligger i
/// <see cref="ElectionPartyResultDto"/> och de två får aldrig presenteras som samma sak.
/// </summary>
public sealed record ElectionForecastPartyDto(
    string PartyCode,
    decimal ForecastShare,
    decimal Lower90,
    decimal Upper90,
    // Sannolikheten att partiet når riksdagsspärren, 0-1.
    decimal ProbabilityAboveThreshold);

/// <summary>
/// Prognosen med det underlag den vilar på. Är fältet null i svaret finns ingen prognos att
/// visa – antingen för att den är avstängd eller för att modellen avstått.
/// </summary>
public sealed record ElectionForecastDto(
    IReadOnlyList<ElectionForecastPartyDto> Parties,
    // "low", "medium", "high" eller "veryHigh", härlett ur intervallbredden.
    string Confidence,
    // Halva bredden på ett typiskt 90 %-intervall, i procentenheter.
    decimal TypicalUncertaintyPoints,
    decimal CoveragePercent,
    // Hur geografiskt skevt underlaget är. 0 = de rapporterade distrikten speglar landet.
    decimal RegionalSkewPercent,
    int ComparableDistrictsUsed,
    string ModelVersion,
    int Draws);

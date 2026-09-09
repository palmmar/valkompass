namespace Valkompass.Application.Election.Nowcast;

/// <summary>
/// Kvalitativ etikett för hur säker prognosen är. Härledd ur modellens faktiska
/// intervallbredd – aldrig ur klockslag.
/// </summary>
public enum NowcastConfidence
{
    Low,
    Medium,
    High,
    VeryHigh,
}

/// <summary>
/// Ett partis prognos. <see cref="ObservedShare"/> är faktiskt räknat och
/// <see cref="ForecastShare"/> är modellens uppskattning – de är alltid skilda fält.
/// </summary>
public sealed record PartyForecast(
    string PartyCode,
    decimal ObservedShare,
    decimal ForecastShare,
    decimal Lower90,
    decimal Upper90,
    // Sannolikheten att partiet når riksdagsspärren, som andel av simuleringarna.
    decimal ProbabilityAboveThreshold);

/// <summary>Vad prognosen bygger på, så att den går att granska i efterhand.</summary>
public sealed record NowcastMetadata(
    string ModelVersion,
    int ComparableDistrictsUsed,
    decimal CoveragePercent,
    decimal BaselineSharePercent,
    int Draws,
    int Seed,
    // Halva bredden på ett typiskt 90 %-intervall, i procentenheter.
    decimal TypicalUncertaintyPoints,
    NowcastConfidence Confidence);

/// <summary>
/// Modellens utdata. Är <see cref="Available"/> falsk finns ingen prognos att visa, och
/// <see cref="UnavailableReason"/> säger varför – att avstå är ett giltigt svar.
/// </summary>
public sealed record NowcastResult(
    bool Available,
    string? UnavailableReason,
    IReadOnlyList<PartyForecast> Parties,
    NowcastMetadata? Metadata)
{
    public static NowcastResult Unavailable(string reason) => new(false, reason, [], null);
}

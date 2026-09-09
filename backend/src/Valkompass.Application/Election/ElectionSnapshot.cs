namespace Valkompass.Application.Election;

// Normaliserad representation av ett rösträkningsläge från Valmyndigheten. Rena värden,
// frikopplade från filformatet – frontend och prognosmotor ska aldrig behöva känna till
// Valmyndighetens JSON-struktur. Se #82.

/// <summary>Vilken räkning siffrorna kommer från.</summary>
public enum CountingStage
{
    /// <summary>Preliminär räkning (valnatten och onsdagens uppsamling).</summary>
    Preliminary,

    /// <summary>Slutlig räkning (länsstyrelsernas kontrollräkning).</summary>
    Final,
}

/// <summary>
/// Härkomst för en snapshot. <see cref="IsTest"/> speglar <c>test</c> i källfilen och är
/// sant för genrepsdata – sådan data får aldrig visas som verkligt valresultat.
/// </summary>
public sealed record ElectionSnapshotSource(
    DateOnly ElectionDate,
    DateOnly PreviousElectionDate,
    CountingStage Stage,
    bool IsTest,
    DateTimeOffset UpdatedAt,
    DateTimeOffset IngestedAt,
    string Checksum,
    int UpdateCount);

/// <summary>
/// Rapporteringsgrad. Distriktandelen och röstberättigandeandelen är olika saker och ska
/// hållas isär – andelen räknade valdistrikt är inte "andel av rösterna räknade".
/// </summary>
public sealed record ElectionReporting(
    int DistrictsReported,
    int DistrictsTotal,
    int EligibleVotersCovered,
    int EligibleVotersTotal,
    int TotalVotes,
    int? TotalVotesPrevious,
    decimal? TurnoutPercent,
    decimal? TurnoutPercentPrevious)
{
    /// <summary>Andel röstberättigade i räknade valdistrikt. Null innan något räknats.</summary>
    public decimal? CoveragePercent => EligibleVotersTotal > 0
        ? Math.Round(100m * EligibleVotersCovered / EligibleVotersTotal, 2)
        : null;
}

/// <summary>Ett partis räknade resultat, med Valmyndighetens jämförelse mot föregående val.</summary>
public sealed record PartyResult(
    string Code,
    string Name,
    int DisplayOrder,
    string ColorHex,
    int Votes,
    decimal SharePercent,
    int? VotesPrevious,
    decimal? SharePreviousPercent,
    decimal? ShareChangePoints);

/// <summary>Officiell preliminär mandatfördelning. Vår egen mandatmotor ingår inte i v1.</summary>
public sealed record PartyMandate(
    string Code,
    int Total,
    int Fixed,
    int Levelling,
    int? TotalPrevious,
    int? Change);

/// <summary>Röster på partier utanför de åtta riksdagspartierna, som en klump.</summary>
public sealed record OtherPartiesResult(
    int Votes,
    decimal SharePercent,
    int? VotesPrevious,
    decimal? SharePreviousPercent);

/// <summary>
/// Ett komplett, självbärande rösträkningsläge. Innehåller bara faktiskt räknat resultat –
/// prognos är en separat sak och blandas aldrig in här (#84).
/// </summary>
public sealed record ElectionSnapshot(
    ElectionSnapshotSource Source,
    ElectionReporting Reporting,
    IReadOnlyList<PartyResult> Results,
    IReadOnlyList<PartyMandate> Mandates,
    OtherPartiesResult OtherParties,
    decimal ThresholdPercent,
    decimal ConstituencyThresholdPercent);

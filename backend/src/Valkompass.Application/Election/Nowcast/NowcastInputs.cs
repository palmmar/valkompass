namespace Valkompass.Application.Election.Nowcast;

/// <summary>Hur ett valdistrikt förhåller sig till 2022 års distriktsindelning.</summary>
public enum DistrictComparison
{
    /// <summary>Motsvarar ett distrikt 2022 – swingen går att räkna direkt.</summary>
    Comparable,

    /// <summary>Motsvarar flera distrikt 2022, summerade av Valmyndigheten.</summary>
    Aggregated,

    /// <summary>
    /// Saknar jämförbar motsvarighet. Hit hör bland annat uppsamlingsdistrikten, där
    /// förtids- och brevröster räknas – de har systematiskt annan partisammansättning än
    /// vallokalsrösterna och får aldrig behandlas som ett vanligt distrikt.
    /// </summary>
    NotComparable,
}

/// <summary>Ett partis röster i ett område, med Valmyndighetens jämförelse mot 2022.</summary>
public sealed record PartyVotes(string PartyCode, int Votes, int? VotesPrevious);

/// <summary>
/// Ett valdistrikt. <see cref="Votes"/> är röster som påverkar mandatfördelningen, vilket är
/// samma nämnare som Valmyndigheten själva räknar sina andelar på.
/// </summary>
public sealed record DistrictSnapshot(
    string Code,
    string MunicipalityCode,
    string CountyCode,
    bool IsReported,
    DistrictComparison Comparison,
    int? EligibleVoters,
    int Votes,
    int? VotesPrevious,
    IReadOnlyList<PartyVotes> Parties)
{
    /// <summary>Går att räkna swing på: rapporterat, jämförbart och med röster i båda valen.</summary>
    public bool CanMeasureSwing =>
        IsReported
        && Comparison != DistrictComparison.NotComparable
        && Votes > 0
        && VotesPrevious is > 0;
}

/// <summary>
/// En kommun. 2022-siffrorna används som fast baseline för hur många röster som återstår att
/// räkna, eftersom distriktsnivån saknar 2022-data för ungefär en femtedel av rösterna.
/// </summary>
public sealed record MunicipalitySnapshot(
    string Code,
    string CountyCode,
    int EligibleVoters,
    int DistrictsReported,
    int DistrictsTotal,
    int Votes,
    int? VotesPrevious,
    IReadOnlyList<PartyVotes> Parties);

/// <summary>Allt prognosmodellen behöver, utan koppling till filformatet.</summary>
public sealed record NowcastInput(
    IReadOnlyList<DistrictSnapshot> Districts,
    IReadOnlyList<MunicipalitySnapshot> Municipalities,
    // Rikets totala antal röster 2022, ur mandatfördelningsfilen.
    int NationalVotesPrevious);

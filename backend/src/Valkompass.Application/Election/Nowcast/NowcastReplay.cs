namespace Valkompass.Application.Election.Nowcast;

/// <summary>I vilken ordning valdistrikten rapporterar in.</summary>
public enum ReportingOrder
{
    Random,

    /// <summary>Stora distrikt först – tätorterna hinner före.</summary>
    LargestFirst,

    /// <summary>Små distrikt först, vilket i praktiken betyder landsbygden.</summary>
    SmallestFirst,

    /// <summary>Län för län, alltså kraftigt geografiskt skev rapportering.</summary>
    ByCounty,

    /// <summary>
    /// Vallokalsröster först och uppsamlingsdistrikten sist. Närmast hur en riktig valnatt
    /// ser ut, eftersom sent inkomna förtidsröster räknas på onsdagen.
    /// </summary>
    PostalLast,
}

/// <summary>
/// Spelar upp ett färdigräknat val som om distrikten rapporterade in successivt. Gör det
/// möjligt att mäta prognosen mot ett känt facit i stället för att upptäcka på valnatten att
/// intervallen var för smala (#87).
/// </summary>
public static class NowcastReplay
{
    /// <summary>Distrikten i den ordning de ska rapportera.</summary>
    public static IReadOnlyList<DistrictSnapshot> Order(
        IReadOnlyList<DistrictSnapshot> districts,
        ReportingOrder order,
        int seed = 1)
    {
        ArgumentNullException.ThrowIfNull(districts);
        var random = new Random(seed);

        return order switch
        {
            ReportingOrder.LargestFirst => [.. districts.OrderByDescending(d => d.Votes)],
            ReportingOrder.SmallestFirst => [.. districts.OrderBy(d => d.Votes)],
            ReportingOrder.ByCounty =>
                [.. districts.OrderBy(d => d.CountyCode, StringComparer.Ordinal).ThenBy(_ => random.Next())],
            ReportingOrder.PostalLast =>
                [.. districts
                    .OrderBy(d => d.Comparison == DistrictComparison.NotComparable && d.EligibleVoters is null ? 1 : 0)
                    .ThenBy(_ => random.Next())],
            _ => [.. districts.OrderBy(_ => random.Next())],
        };
    }

    /// <summary>
    /// Bygger indata som det hade sett ut när <paramref name="targetShareOfVotes"/> av rösterna
    /// hunnit räknas, givet en rapporteringsordning.
    /// </summary>
    /// <remarks>
    /// Kommunernas 2022-siffror lämnas orörda medan 2026-rösterna byggs upp distrikt för
    /// distrikt. Det speglar antagandet modellen vilar på: att den historiska baselinen är hel
    /// från början och inte växer fram i takt med rösträkningen.
    /// </remarks>
    public static NowcastInput Reveal(
        IReadOnlyList<DistrictSnapshot> ordered,
        IReadOnlyList<MunicipalitySnapshot> municipalities,
        int nationalVotesPrevious,
        double targetShareOfVotes)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        ArgumentNullException.ThrowIfNull(municipalities);

        var totalVotes = ordered.Sum(d => (long)d.Votes);
        var target = totalVotes * Math.Clamp(targetShareOfVotes, 0, 1);

        var revealed = new HashSet<string>(StringComparer.Ordinal);
        long running = 0;
        foreach (var district in ordered)
        {
            if (running >= target)
            {
                break;
            }

            revealed.Add(district.Code);
            running += district.Votes;
        }

        return Reveal(ordered, municipalities, nationalVotesPrevious, revealed);
    }

    /// <summary>Bygger indata där exakt de angivna distrikten har rapporterat.</summary>
    public static NowcastInput Reveal(
        IReadOnlyList<DistrictSnapshot> districts,
        IReadOnlyList<MunicipalitySnapshot> municipalities,
        int nationalVotesPrevious,
        IReadOnlySet<string> revealedCodes)
    {
        var districtStates = new List<DistrictSnapshot>(districts.Count);
        var counted = new Dictionary<string, Tally>(StringComparer.Ordinal);

        foreach (var district in districts)
        {
            if (!revealedCodes.Contains(district.Code))
            {
                // Ett orapporterat distrikt har inga röster ännu. 2022-siffrorna får ligga
                // kvar; de används ändå inte förrän distriktet rapporterat.
                districtStates.Add(district with
                {
                    IsReported = false,
                    Votes = 0,
                    Parties = [.. district.Parties.Select(p => p with { Votes = 0 })],
                });
                continue;
            }

            districtStates.Add(district with { IsReported = true });

            if (!counted.TryGetValue(district.MunicipalityCode, out var tally))
            {
                tally = new Tally();
                counted[district.MunicipalityCode] = tally;
            }

            tally.Districts++;
            tally.Votes += district.Votes;
            foreach (var party in district.Parties)
            {
                tally.Parties[party.PartyCode] = tally.Parties.GetValueOrDefault(party.PartyCode) + party.Votes;
            }
        }

        var municipalityStates = municipalities.Select(m =>
        {
            var tally = counted.GetValueOrDefault(m.Code) ?? new Tally();
            return m with
            {
                Votes = tally.Votes,
                DistrictsReported = tally.Districts,
                Parties = [.. m.Parties.Select(p => p with { Votes = tally.Parties.GetValueOrDefault(p.PartyCode) })],
            };
        }).ToList();

        return new NowcastInput(districtStates, municipalityStates, nationalVotesPrevious);
    }

    private sealed class Tally
    {
        public int Districts;
        public int Votes;
        public Dictionary<string, int> Parties { get; } = new(StringComparer.Ordinal);
    }
}

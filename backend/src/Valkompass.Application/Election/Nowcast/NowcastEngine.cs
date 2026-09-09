namespace Valkompass.Application.Election.Nowcast;

/// <summary>
/// Prognosmodellen: uppskattar det slutliga nationella resultatet utifrån de valdistrikt som
/// hunnit rapportera.
/// </summary>
/// <remarks>
/// <para>
/// Grundidén är att inte anta att de först räknade distrikten är representativa för Sverige.
/// I stället mäts <em>förändringen</em> sedan 2022 i de distrikt som rapporterat, och den
/// förändringen används för att uppskatta de distrikt som återstår.
/// </para>
/// <para>
/// Arbetsdelningen mellan nivåerna följer var datan faktiskt räcker till. Swingen mäts per
/// valdistrikt, där parningen mot 2022 är entydig – samma distrikt, båda valen. Volymen, alltså
/// hur många röster som återstår att räkna, modelleras per kommun, eftersom distriktsnivån
/// saknar 2022-siffror för drygt en femtedel av rösterna. Uppsamlingsdistriktens förtids- och
/// brevröster blir därmed en del av kommunens återstående röster i stället för att behöva en
/// baseline de inte kan få.
/// </para>
/// <para>
/// Lokala swingar shrinkas mot överliggande nivå i proportion till hur mycket underlag de
/// vilar på, så att några få tidiga distrikt inte får orimligt genomslag.
/// </para>
/// </remarks>
public static class NowcastEngine
{
    public const string ModelVersion = "nowcast-1";

    public static NowcastResult Run(NowcastInput input, NowcastOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var opts = options ?? new NowcastOptions();

        var ctx = Context.Build(input, opts.ShrinkageVotes);
        if (ctx is null)
        {
            return NowcastResult.Unavailable("Underlaget saknar kommun- eller partidata.");
        }

        // Kommunernas 2022-röster används som fast baseline för hur mycket som återstår. Om
        // summan ligger långt under rikets kända total är baselinen räknad bara på det som
        // hunnit rapporteras, och då håller inte modellens antagande.
        var baselineShare = input.NationalVotesPrevious > 0
            ? ctx.TotalVotesPrevious / input.NationalVotesPrevious
            : 0;
        if (baselineShare < opts.MinimumBaselineShare)
        {
            return NowcastResult.Unavailable(
                "Kommunernas historiska underlag är ofullständigt, så prognosen kan inte göras.");
        }

        if (ctx.MeasuredCount < opts.MinimumComparableDistricts)
        {
            return NowcastResult.Unavailable("För få jämförbara valdistrikt har rapporterat.");
        }

        var scratch = new Scratch(ctx);
        var identity = Enumerable.Range(0, ctx.MeasuredCount).ToArray();
        var noShock = new double[ctx.PartyCount];

        // Punktskattningen körs utan omsampling och utan brus – samma indata ger samma svar.
        var point = Project(ctx, scratch, identity, noShock);
        var coverage = point.Coverage;

        if (coverage < opts.MinimumCoverage)
        {
            return NowcastResult.Unavailable("För liten andel av rösterna är räknade.");
        }

        var draws = Simulate(ctx, scratch, opts, coverage);
        return Summarise(ctx, opts, point, draws, coverage, baselineShare);
    }

    // --- Simulering ---

    /// <summary>
    /// Bootstrap över de rapporterade distrikten, plus en systematisk stöt som fångar att de
    /// som rapporterat inte behöver likna dem som återstår.
    /// </summary>
    private static double[][] Simulate(Context ctx, Scratch scratch, NowcastOptions opts, double coverage)
    {
        var random = new Random(opts.Seed);
        var remaining = Math.Max(0, 1 - coverage);
        var sigma = opts.SystematicSwingSigma * Math.Sqrt(remaining);

        var draws = new double[opts.Draws][];
        var sample = new int[ctx.MeasuredCount];
        var shock = new double[ctx.PartyCount];

        for (var d = 0; d < opts.Draws; d++)
        {
            for (var i = 0; i < sample.Length; i++)
            {
                sample[i] = random.Next(ctx.MeasuredCount);
            }

            for (var p = 0; p < shock.Length; p++)
            {
                shock[p] = NextNormal(random) * sigma;
            }

            draws[d] = Project(ctx, scratch, sample, shock).Shares;
        }

        return draws;
    }

    /// <summary>Box–Muller. Egen implementation för att hålla resultatet reproducerbart.</summary>
    private static double NextNormal(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    // --- Projektionen ---

    private readonly record struct Projection(double[] Shares, double Coverage);

    private static Projection Project(Context ctx, Scratch s, int[] sample, double[] shock)
    {
        var n = ctx.PartyCount;
        var slots = n + 1; // partierna plus en klump för övriga

        Array.Clear(s.MunicipalityNow);
        Array.Clear(s.MunicipalityThen);
        Array.Clear(s.MunicipalityNowTotal);
        Array.Clear(s.MunicipalityThenTotal);

        // 1. Summera de rapporterade distriktens röster per kommun, båda valen.
        foreach (var index in sample)
        {
            var k = ctx.DistrictMunicipality[index];
            s.MunicipalityNowTotal[k] += ctx.DistrictVotes[index];
            s.MunicipalityThenTotal[k] += ctx.DistrictVotesPrevious[index];

            var from = index * slots;
            var to = k * slots;
            for (var p = 0; p < slots; p++)
            {
                s.MunicipalityNow[to + p] += ctx.DistrictPartyVotes[from + p];
                s.MunicipalityThen[to + p] += ctx.DistrictPartyVotesPrevious[from + p];
            }
        }

        // 2. Rulla upp till län och riket.
        Array.Clear(s.CountyNow);
        Array.Clear(s.CountyThen);
        Array.Clear(s.CountyNowTotal);
        Array.Clear(s.CountyThenTotal);
        Array.Clear(s.NationalNow);
        Array.Clear(s.NationalThen);
        double nationalNowTotal = 0, nationalThenTotal = 0;

        for (var k = 0; k < ctx.MunicipalityCount; k++)
        {
            var c = ctx.MunicipalityCounty[k];
            s.CountyNowTotal[c] += s.MunicipalityNowTotal[k];
            s.CountyThenTotal[c] += s.MunicipalityThenTotal[k];
            nationalNowTotal += s.MunicipalityNowTotal[k];
            nationalThenTotal += s.MunicipalityThenTotal[k];

            var from = k * slots;
            var to = c * slots;
            for (var p = 0; p < slots; p++)
            {
                s.CountyNow[to + p] += s.MunicipalityNow[from + p];
                s.CountyThen[to + p] += s.MunicipalityThen[from + p];
                s.NationalNow[p] += s.MunicipalityNow[from + p];
                s.NationalThen[p] += s.MunicipalityThen[from + p];
            }
        }

        // 3. Swing och volymkvot per nivå, med shrinkage nedåt.
        var nationalSwing = new double[slots];
        for (var p = 0; p < slots; p++)
        {
            nationalSwing[p] = Share(s.NationalNow[p], nationalNowTotal) - Share(s.NationalThen[p], nationalThenTotal);
        }

        var nationalRatio = nationalThenTotal > 0 ? nationalNowTotal / nationalThenTotal : 1.0;

        for (var c = 0; c < ctx.CountyCount; c++)
        {
            var weight = Weight(s.CountyThenTotal[c], ctx.Shrinkage);
            var from = c * slots;
            for (var p = 0; p < slots; p++)
            {
                var local = Share(s.CountyNow[from + p], s.CountyNowTotal[c])
                            - Share(s.CountyThen[from + p], s.CountyThenTotal[c]);
                s.CountySwing[from + p] = weight * local + (1 - weight) * nationalSwing[p];
            }

            var localRatio = s.CountyThenTotal[c] > 0 ? s.CountyNowTotal[c] / s.CountyThenTotal[c] : nationalRatio;
            s.CountyRatio[c] = weight * localRatio + (1 - weight) * nationalRatio;
        }

        double projectedVotes = 0, countedVotes = 0;
        Array.Clear(s.PartyVotes);

        for (var k = 0; k < ctx.MunicipalityCount; k++)
        {
            var c = ctx.MunicipalityCounty[k];
            var weight = Weight(s.MunicipalityThenTotal[k], ctx.Shrinkage);
            var from = k * slots;

            // 4. Hur många röster kommunen väntas ge totalt, och hur många som återstår.
            var localRatio = s.MunicipalityThenTotal[k] > 0
                ? s.MunicipalityNowTotal[k] / s.MunicipalityThenTotal[k]
                : s.CountyRatio[c];
            var ratio = weight * localRatio + (1 - weight) * s.CountyRatio[c];

            var counted = ctx.MunicipalityVotes[k];
            var expected = ctx.MunicipalityVotesPrevious[k] * ratio;
            var remaining = Math.Max(0, expected - counted);

            countedVotes += counted;
            projectedVotes += counted + remaining;

            // 5. Partifördelningen bland de återstående rösterna: kommunens 2022-fördelning
            //    plus den skattade förändringen.
            double sum = 0;
            for (var p = 0; p < slots; p++)
            {
                var local = Share(s.MunicipalityNow[from + p], s.MunicipalityNowTotal[k])
                            - Share(s.MunicipalityThen[from + p], s.MunicipalityThenTotal[k]);
                var swing = weight * local + (1 - weight) * s.CountySwing[c * slots + p];

                var baseline = ctx.MunicipalityPartyShare[from + p];
                var projected = baseline + swing + (p < shock.Length ? shock[p] : 0);
                s.Projected[p] = Math.Max(0, projected);
                sum += s.Projected[p];
            }

            for (var p = 0; p < slots; p++)
            {
                // Normaliseras så att kommunens andelar summerar till ett.
                var share = sum > 0 ? s.Projected[p] / sum : ctx.MunicipalityPartyShare[from + p];
                s.PartyVotes[p] += ctx.MunicipalityPartyVotes[from + p] + remaining * share;
            }
        }

        var shares = new double[ctx.PartyCount];
        for (var p = 0; p < ctx.PartyCount; p++)
        {
            shares[p] = projectedVotes > 0 ? s.PartyVotes[p] / projectedVotes : 0;
        }

        return new Projection(shares, projectedVotes > 0 ? countedVotes / projectedVotes : 0);
    }

    private static double Share(double part, double total) => total > 0 ? part / total : 0;

    /// <summary>Hur mycket den lokala mätningen får väga mot den överliggande nivån.</summary>
    private static double Weight(double support, double shrinkage) =>
        support <= 0 ? 0 : support / (support + shrinkage);

    // --- Sammanställning ---

    private static NowcastResult Summarise(
        Context ctx,
        NowcastOptions opts,
        Projection point,
        double[][] draws,
        double coverage,
        double baselineShare)
    {
        var parties = new List<PartyForecast>(ctx.PartyCount);
        var halfWidths = new List<double>(ctx.PartyCount);
        var column = new double[draws.Length];

        for (var p = 0; p < ctx.PartyCount; p++)
        {
            for (var d = 0; d < draws.Length; d++)
            {
                column[d] = draws[d][p];
            }

            Array.Sort(column);
            var lower = Percentile(column, 0.05);
            var upper = Percentile(column, 0.95);

            var aboveThreshold = 0;
            foreach (var value in column)
            {
                if (value >= 0.04)
                {
                    aboveThreshold++;
                }
            }

            halfWidths.Add((upper - lower) / 2);

            parties.Add(new PartyForecast(
                PartyCode: ctx.PartyCodes[p],
                ObservedShare: Percent(ctx.ObservedShares[p]),
                ForecastShare: Percent(point.Shares[p]),
                Lower90: Percent(lower),
                Upper90: Percent(upper),
                ProbabilityAboveThreshold: Math.Round((decimal)aboveThreshold / draws.Length, 3)));
        }

        halfWidths.Sort();
        var typical = halfWidths.Count > 0 ? halfWidths[halfWidths.Count / 2] * 100 : 0;

        var metadata = new NowcastMetadata(
            ModelVersion: ModelVersion,
            ComparableDistrictsUsed: ctx.MeasuredCount,
            CoveragePercent: Math.Round((decimal)coverage * 100, 2),
            BaselineSharePercent: Math.Round((decimal)baselineShare * 100, 2),
            Draws: opts.Draws,
            Seed: opts.Seed,
            TypicalUncertaintyPoints: Math.Round((decimal)typical, 2),
            Confidence: Classify(typical));

        return new NowcastResult(true, null, parties, metadata);
    }

    /// <summary>Etiketten följer intervallbredden, inte hur mycket klockan är.</summary>
    private static NowcastConfidence Classify(double halfWidthPoints) => halfWidthPoints switch
    {
        > 2.0 => NowcastConfidence.Low,
        > 1.0 => NowcastConfidence.Medium,
        > 0.4 => NowcastConfidence.High,
        _ => NowcastConfidence.VeryHigh,
    };

    private static double Percentile(double[] sorted, double q)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        var position = q * (sorted.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (position - lower) * (sorted[upper] - sorted[lower]);
    }

    private static decimal Percent(double fraction) => Math.Round((decimal)fraction * 100, 2);

    // --- Förberedd indata i platta arrayer, för att simuleringen ska gå snabbt ---

    private sealed class Context
    {
        public required string[] PartyCodes { get; init; }
        public required int PartyCount { get; init; }
        public required int MunicipalityCount { get; init; }
        public required int CountyCount { get; init; }
        public required int MeasuredCount { get; init; }
        public required double Shrinkage { get; init; }

        public required int[] DistrictMunicipality { get; init; }
        public required double[] DistrictVotes { get; init; }
        public required double[] DistrictVotesPrevious { get; init; }
        public required double[] DistrictPartyVotes { get; init; }
        public required double[] DistrictPartyVotesPrevious { get; init; }

        public required int[] MunicipalityCounty { get; init; }
        public required double[] MunicipalityVotes { get; init; }
        public required double[] MunicipalityVotesPrevious { get; init; }
        public required double[] MunicipalityPartyVotes { get; init; }
        public required double[] MunicipalityPartyShare { get; init; }

        public required double TotalVotesPrevious { get; init; }
        public required double[] ObservedShares { get; init; }

        public static Context? Build(NowcastInput input, double shrinkage = 10_000)
        {
            var codes = input.Municipalities
                .SelectMany(m => m.Parties.Select(p => p.PartyCode))
                .Distinct()
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToArray();

            if (codes.Length == 0 || input.Municipalities.Count == 0)
            {
                return null;
            }

            var partyIndex = codes.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
            var slots = codes.Length + 1;
            var other = codes.Length;

            var counties = input.Municipalities
                .Select(m => m.CountyCode)
                .Distinct()
                .OrderBy(c => c, StringComparer.Ordinal)
                .Select((c, i) => (c, i))
                .ToDictionary(x => x.c, x => x.i);

            var municipalityIndex = new Dictionary<string, int>(input.Municipalities.Count);
            var mCounty = new int[input.Municipalities.Count];
            var mVotes = new double[input.Municipalities.Count];
            var mPrevious = new double[input.Municipalities.Count];
            var mPartyVotes = new double[input.Municipalities.Count * slots];
            var mPartyShare = new double[input.Municipalities.Count * slots];
            var observed = new double[codes.Length];
            double observedTotal = 0, totalPrevious = 0;

            for (var k = 0; k < input.Municipalities.Count; k++)
            {
                var m = input.Municipalities[k];
                municipalityIndex[m.Code] = k;
                mCounty[k] = counties.GetValueOrDefault(m.CountyCode, 0);
                mVotes[k] = m.Votes;
                mPrevious[k] = m.VotesPrevious ?? 0;
                totalPrevious += mPrevious[k];
                observedTotal += m.Votes;

                double partySum = 0, previousSum = 0;
                foreach (var party in m.Parties)
                {
                    if (!partyIndex.TryGetValue(party.PartyCode, out var p))
                    {
                        continue;
                    }

                    mPartyVotes[k * slots + p] = party.Votes;
                    observed[p] += party.Votes;
                    partySum += party.Votes;

                    var previous = party.VotesPrevious ?? 0;
                    mPartyShare[k * slots + p] = mPrevious[k] > 0 ? previous / mPrevious[k] : 0;
                    previousSum += previous;
                }

                mPartyVotes[k * slots + other] = Math.Max(0, m.Votes - partySum);
                mPartyShare[k * slots + other] =
                    mPrevious[k] > 0 ? Math.Max(0, (mPrevious[k] - previousSum) / mPrevious[k]) : 0;
            }

            var measured = input.Districts.Where(d => d.CanMeasureSwing).ToList();
            var dMunicipality = new int[measured.Count];
            var dVotes = new double[measured.Count];
            var dPrevious = new double[measured.Count];
            var dPartyVotes = new double[measured.Count * slots];
            var dPartyPrevious = new double[measured.Count * slots];

            for (var i = 0; i < measured.Count; i++)
            {
                var d = measured[i];
                dMunicipality[i] = municipalityIndex.GetValueOrDefault(d.MunicipalityCode, 0);
                dVotes[i] = d.Votes;
                dPrevious[i] = d.VotesPrevious ?? 0;

                double partySum = 0, previousSum = 0;
                foreach (var party in d.Parties)
                {
                    if (!partyIndex.TryGetValue(party.PartyCode, out var p))
                    {
                        continue;
                    }

                    dPartyVotes[i * slots + p] = party.Votes;
                    dPartyPrevious[i * slots + p] = party.VotesPrevious ?? 0;
                    partySum += party.Votes;
                    previousSum += party.VotesPrevious ?? 0;
                }

                dPartyVotes[i * slots + other] = Math.Max(0, dVotes[i] - partySum);
                dPartyPrevious[i * slots + other] = Math.Max(0, dPrevious[i] - previousSum);
            }

            for (var p = 0; p < codes.Length; p++)
            {
                observed[p] = observedTotal > 0 ? observed[p] / observedTotal : 0;
            }

            return new Context
            {
                PartyCodes = codes,
                PartyCount = codes.Length,
                MunicipalityCount = input.Municipalities.Count,
                CountyCount = counties.Count,
                MeasuredCount = measured.Count,
                Shrinkage = shrinkage,
                DistrictMunicipality = dMunicipality,
                DistrictVotes = dVotes,
                DistrictVotesPrevious = dPrevious,
                DistrictPartyVotes = dPartyVotes,
                DistrictPartyVotesPrevious = dPartyPrevious,
                MunicipalityCounty = mCounty,
                MunicipalityVotes = mVotes,
                MunicipalityVotesPrevious = mPrevious,
                MunicipalityPartyVotes = mPartyVotes,
                MunicipalityPartyShare = mPartyShare,
                TotalVotesPrevious = totalPrevious,
                ObservedShares = observed,
            };
        }
    }

    /// <summary>Buffertar som återanvänds mellan simuleringarna för att slippa allokera.</summary>
    private sealed class Scratch(Context ctx)
    {
        public double[] MunicipalityNow { get; } = new double[ctx.MunicipalityCount * (ctx.PartyCount + 1)];
        public double[] MunicipalityThen { get; } = new double[ctx.MunicipalityCount * (ctx.PartyCount + 1)];
        public double[] MunicipalityNowTotal { get; } = new double[ctx.MunicipalityCount];
        public double[] MunicipalityThenTotal { get; } = new double[ctx.MunicipalityCount];

        public double[] CountyNow { get; } = new double[ctx.CountyCount * (ctx.PartyCount + 1)];
        public double[] CountyThen { get; } = new double[ctx.CountyCount * (ctx.PartyCount + 1)];
        public double[] CountyNowTotal { get; } = new double[ctx.CountyCount];
        public double[] CountyThenTotal { get; } = new double[ctx.CountyCount];
        public double[] CountySwing { get; } = new double[ctx.CountyCount * (ctx.PartyCount + 1)];
        public double[] CountyRatio { get; } = new double[ctx.CountyCount];

        public double[] NationalNow { get; } = new double[ctx.PartyCount + 1];
        public double[] NationalThen { get; } = new double[ctx.PartyCount + 1];

        public double[] Projected { get; } = new double[ctx.PartyCount + 1];
        public double[] PartyVotes { get; } = new double[ctx.PartyCount + 1];
    }
}

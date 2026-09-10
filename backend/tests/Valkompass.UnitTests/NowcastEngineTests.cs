using Valkompass.Application.Election.Nowcast;

namespace Valkompass.UnitTests;

/// <summary>
/// Modellen körd mot genrepets 6 626 valdistrikt, uppspelade som om de rapporterade in
/// successivt. Facit är det färdigräknade resultatet, så varje prognos går att mäta.
/// </summary>
public class NowcastEngineTests
{
    private const int NationalVotesPrevious = 6_547_801;

    private static readonly Lazy<IReadOnlyList<DistrictSnapshot>> AllDistricts = new(() =>
        NowcastInputParser.ParseDistricts(ElectionFixtures.ReadZipEntry("rostfordelning_00_RD.json")));

    private static readonly Lazy<IReadOnlyList<MunicipalitySnapshot>> AllMunicipalities = new(() =>
        NowcastInputParser.ParseMunicipalities(ElectionFixtures.ReadZipEntry("summering_RD.json")));

    /// <summary>Det färdigräknade resultatet: partiets andel av alla röster.</summary>
    private static IReadOnlyDictionary<string, double> FinalShares()
    {
        var total = (double)AllMunicipalities.Value.Sum(m => (long)m.Votes);
        return AllMunicipalities.Value
            .SelectMany(m => m.Parties)
            .GroupBy(p => p.PartyCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(p => (long)p.Votes) / total * 100, StringComparer.Ordinal);
    }

    private static NowcastInput At(double coverage, ReportingOrder order = ReportingOrder.Random, int seed = 1)
    {
        var ordered = NowcastReplay.Order(AllDistricts.Value, order, seed);
        return NowcastReplay.Reveal(ordered, AllMunicipalities.Value, NationalVotesPrevious, coverage);
    }

    [Fact]
    public void Fardigraknat_ger_prognos_lika_med_det_raknade()
    {
        var result = NowcastEngine.Run(At(1.0));

        Assert.True(result.Available);
        Assert.All(result.Parties, p =>
            Assert.True(Math.Abs(p.ForecastShare - p.ObservedShare) < 0.05m,
                $"{p.PartyCode}: prognos {p.ForecastShare} mot räknat {p.ObservedShare}"));
    }

    [Fact]
    public void Samma_indata_och_fro_ger_samma_resultat()
    {
        var a = NowcastEngine.Run(At(0.25));
        var b = NowcastEngine.Run(At(0.25));

        Assert.Equal(
            a.Parties.Select(p => (p.PartyCode, p.ForecastShare, p.Lower90, p.Upper90)),
            b.Parties.Select(p => (p.PartyCode, p.ForecastShare, p.Lower90, p.Upper90)));
    }

    [Fact]
    public void Olika_fro_ger_nastan_samma_intervall()
    {
        // Simuleringsbruset ska vara litet nog att inte flytta intervallen märkbart.
        var a = NowcastEngine.Run(At(0.25));
        var b = NowcastEngine.Run(At(0.25), new NowcastOptions { Seed = 987 });

        foreach (var (x, y) in a.Parties.Zip(b.Parties))
        {
            Assert.True(Math.Abs(x.Lower90 - y.Lower90) < 0.3m, $"{x.PartyCode}: {x.Lower90} mot {y.Lower90}");
        }
    }

    [Fact]
    public void For_fa_rapporterade_distrikt_ger_ingen_prognos()
    {
        var result = NowcastEngine.Run(At(0.001));

        Assert.False(result.Available);
        Assert.NotNull(result.UnavailableReason);
        Assert.Empty(result.Parties);
    }

    [Fact]
    public void Ofullstandig_historisk_baseline_ger_ingen_prognos()
    {
        // Om kommunernas 2022-siffror bara skulle täcka det som hunnit räknas håller inte
        // modellens antagande, och då ska den avstå i stället för att gissa.
        var input = At(0.5) with { NationalVotesPrevious = NationalVotesPrevious * 4 };

        var result = NowcastEngine.Run(input);

        Assert.False(result.Available);
        Assert.Contains("historiska", result.UnavailableReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Intervallen_kryper_ihop_nar_mer_hunnit_raknas()
    {
        var early = NowcastEngine.Run(At(0.10)).Metadata!.TypicalUncertaintyPoints;
        var middle = NowcastEngine.Run(At(0.40)).Metadata!.TypicalUncertaintyPoints;
        var late = NowcastEngine.Run(At(0.80)).Metadata!.TypicalUncertaintyPoints;

        Assert.True(middle < early, $"10 %: ±{early}, 40 %: ±{middle}");
        Assert.True(late < middle, $"40 %: ±{middle}, 80 %: ±{late}");
    }

    [Fact]
    public void Uppsamlingsdistrikten_kraschar_inte_modellen()
    {
        // Vallokalsrösterna först, förtidsrösterna sist – de saknar både 2022-underlag och
        // röstberättigade, och är den vanligaste orsaken till att en modell spårar ur.
        var result = NowcastEngine.Run(At(0.6, ReportingOrder.PostalLast));

        Assert.True(result.Available);
        Assert.Equal(8, result.Parties.Count);
        Assert.All(result.Parties, p => Assert.InRange(p.ForecastShare, 0m, 100m));
    }

    [Fact]
    public void Andelarna_summerar_till_under_hundra()
    {
        // De åtta riksdagspartierna plus övriga partier ska rymmas inom hundra procent.
        var sum = NowcastEngine.Run(At(0.3)).Parties.Sum(p => p.ForecastShare);

        Assert.InRange(sum, 90m, 100m);
    }

    [Fact]
    public void Sannolikheten_for_riksdagssparren_ar_hog_for_stora_partier()
    {
        var result = NowcastEngine.Run(At(0.3));

        var s = Assert.Single(result.Parties, p => p.PartyCode == "S");
        Assert.True(s.ProbabilityAboveThreshold > 0.99m, $"S: {s.ProbabilityAboveThreshold}");
    }

    // --- Backtest: prognosen mot ett känt facit ---

    [Theory]
    [InlineData(ReportingOrder.Random)]
    [InlineData(ReportingOrder.LargestFirst)]
    [InlineData(ReportingOrder.SmallestFirst)]
    [InlineData(ReportingOrder.ByCounty)]
    [InlineData(ReportingOrder.PostalLast)]
    public void Felet_krymper_nar_mer_raknats(ReportingOrder order)
    {
        var final = FinalShares();

        var early = MeanAbsoluteError(NowcastEngine.Run(At(0.05, order), PointOnly), final);
        var late = MeanAbsoluteError(NowcastEngine.Run(At(0.50, order), PointOnly), final);

        Assert.True(late <= early + 0.15,
            $"{order}: fel vid 5 % = {early:F2} pp, vid 50 % = {late:F2} pp");
    }

    /// <summary>
    /// Ordningar som liknar en riktig valnatt, där distrikt över hela landet rapporterar
    /// parallellt.
    /// </summary>
    private static readonly ReportingOrder[] RealisticOrders =
    [
        ReportingOrder.Random,
        ReportingOrder.LargestFirst,
        ReportingOrder.SmallestFirst,
        ReportingOrder.PostalLast,
    ];

    [Theory]
    [InlineData(0.10, 2.5)]
    [InlineData(0.25, 1.5)]
    [InlineData(0.50, 1.0)]
    [InlineData(0.75, 0.6)]
    public void Prognosen_ligger_nara_slutresultatet(double coverage, double maxError)
    {
        var final = FinalShares();

        foreach (var order in RealisticOrders)
        {
            var error = MeanAbsoluteError(NowcastEngine.Run(At(coverage, order), PointOnly), final);
            Assert.True(error <= maxError,
                $"{order} vid {coverage:P0}: medelfel {error:F2} pp, tak {maxError:F2} pp");
        }
    }

    [Theory]
    [InlineData(0.10, 3.0)]
    [InlineData(0.25, 2.5)]
    [InlineData(0.50, 1.0)]
    [InlineData(0.75, 0.6)]
    public void Lan_for_lan_ar_samre_men_sparar_inte_ur(double coverage, double maxError)
    {
        // Extremfallet: hela län rapporterar innan andra har börjat. Vid 25 % är det i
        // praktiken bara Stockholm som räknats, och hela landets swing skattas därifrån.
        // Så ser en riktig valnatt inte ut - distrikt över hela landet rapporterar parallellt -
        // men modellen ska degradera kontrollerat och inte spåra ur. Uppmätt: 2,7 pp vid 10 %
        // och 2,3 pp vid 25 %, mot omkring 1 pp för de realistiska ordningarna.
        var final = FinalShares();

        var error = MeanAbsoluteError(NowcastEngine.Run(At(coverage, ReportingOrder.ByCounty), PointOnly), final);

        Assert.True(error <= maxError,
            $"ByCounty vid {coverage:P0}: medelfel {error:F2} pp, tak {maxError:F2} pp");
    }

    [Fact]
    public void Nittioprocentsintervallen_traffar_ungefar_nittio_procent()
    {
        // Kalibreringen: hur ofta ligger det verkliga slutresultatet i det intervall vi visar?
        // Ligger andelen långt under 90 % är modellen överkonfident, och då ska ingen
        // sannolikhet för riksdagsspärren publiceras.
        var rate = IntervalHitRate(RealisticOrders);

        // Ett 90 %-intervall ska träffa ungefär 90 %. För lågt betyder överkonfident, för
        // högt betyder onödigt breda intervall - båda är fel, så bandet är tvåsidigt.
        Assert.True(rate is >= 0.86 and <= 0.99, $"90 %-intervallen träffade {rate:P1}.");
    }

    [Fact]
    public void Intervallen_haller_hjalpligt_aven_lan_for_lan()
    {
        // Kalibreringen görs mot realistiska rapporteringsordningar. Vid extrem geografisk
        // skevhet ar punktskattningen samre, och da ska intervallen inte kollapsa - men de
        // kan inte heller behova rymma ett scenario som inte intraffar, for da blir de for
        // vida alla andra kvallar.
        var rate = IntervalHitRate([ReportingOrder.ByCounty]);

        Assert.True(rate >= 0.65, $"lan for lan: intervallen traffade {rate:P1}.");
    }

    /// <summary>Hur ofta det verkliga slutresultatet ligger inom det visade intervallet.</summary>
    private static double IntervalHitRate(ReportingOrder[] orders)
    {
        var final = FinalShares();
        var coverages = new[] { 0.05, 0.10, 0.25, 0.50, 0.75 };

        var hits = 0;
        var total = 0;

        foreach (var order in orders)
        {
            foreach (var coverage in coverages)
            {
                var result = NowcastEngine.Run(At(coverage, order), Calibration);
                if (!result.Available)
                {
                    continue;
                }

                foreach (var party in result.Parties)
                {
                    var truth = (decimal)final[party.PartyCode];
                    total++;
                    if (truth >= party.Lower90 && truth <= party.Upper90)
                    {
                        hits++;
                    }
                }
            }
        }

        return (double)hits / total;
    }

    /// <summary>Få simuleringar: punktskattningen påverkas inte, men testerna går fortare.</summary>
    private static readonly NowcastOptions PointOnly = new() { Draws = 50 };

    private static readonly NowcastOptions Calibration = new() { Draws = 400 };

    private static double MeanAbsoluteError(NowcastResult result, IReadOnlyDictionary<string, double> final)
    {
        Assert.True(result.Available, result.UnavailableReason);
        return result.Parties.Average(p => Math.Abs((double)p.ForecastShare - final[p.PartyCode]));
    }
}

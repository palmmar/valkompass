using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Valkompass.Application.Election;
using Valkompass.Domain.Enums;

namespace Valkompass.UnitTests;

/// <summary>
/// Körs mot Valmyndighetens arkiverade genrepsfil i <c>backend/tests/fixtures/valmyndigheten/</c>.
/// Inga nätverksanrop – genrepet är avslutat och filerna kan försvinna från val.se.
/// </summary>
public class ElectionResultParserTests
{
    private static readonly DateTimeOffset Ingested = new(2026, 9, 9, 18, 0, 0, TimeSpan.FromHours(2));
    private const string Checksum = "3e6407d8abce4a7856a396cd61e02724";

    [Fact]
    public void Parsar_genrepets_mandatfil()
    {
        var snapshot = ParseFixture();

        Assert.Equal(new DateOnly(2026, 9, 13), snapshot.Source.ElectionDate);
        Assert.Equal(new DateOnly(2022, 9, 11), snapshot.Source.PreviousElectionDate);
        Assert.Equal(CountingStage.Preliminary, snapshot.Source.Stage);
        Assert.Equal(Checksum, snapshot.Source.Checksum);
        Assert.Equal(Ingested, snapshot.Source.IngestedAt);
        Assert.Equal(353, snapshot.Source.UpdateCount);
        Assert.Equal(4m, snapshot.ThresholdPercent);
        Assert.Equal(12m, snapshot.ConstituencyThresholdPercent);
    }

    [Fact]
    public void Genrepsdata_ar_alltid_markt_som_test()
    {
        Assert.True(ParseFixture().Source.IsTest);
    }

    [Fact]
    public void Saknad_testflagga_tolkas_som_test()
    {
        // Hellre märka skarp data som test än tvärtom.
        var snapshot = ParseModified(root => root.Remove("test"));

        Assert.True(snapshot.Source.IsTest);
    }

    [Fact]
    public void Ger_de_atta_riksdagspartierna_med_vara_partikoder()
    {
        var snapshot = ParseFixture();

        Assert.Equal(
            new[] { "C", "KD", "L", "M", "MP", "S", "SD", "V" },
            snapshot.Results.Select(r => r.Code).OrderBy(c => c, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Laser_rikets_rostfordelning_med_jamforelse_mot_2022()
    {
        var m = Assert.Single(ParseFixture().Results, r => r.Code == "M");

        Assert.Equal("Moderaterna", m.Name);
        Assert.Equal(1_348_561, m.Votes);
        Assert.Equal(19.6m, m.SharePercent);
        Assert.Equal(1_237_428, m.VotesPrevious);
        Assert.Equal(19.1m, m.SharePreviousPercent);
        Assert.Equal(0.5m, m.ShareChangePoints);
    }

    [Fact]
    public void Laser_officiell_preliminar_mandatfordelning()
    {
        var snapshot = ParseFixture();
        var m = Assert.Single(snapshot.Mandates, x => x.Code == "M");

        Assert.Equal(69, m.Total);
        Assert.Equal(67, m.Fixed);
        Assert.Equal(2, m.Levelling);
        Assert.Equal(68, m.TotalPrevious);
        Assert.Equal(1, m.Change);
        Assert.Equal(349, snapshot.Mandates.Sum(x => x.Total));
    }

    [Fact]
    public void Laser_rapporteringsgrad_bade_i_distrikt_och_rostberattigade()
    {
        var reporting = ParseFixture().Reporting;

        Assert.Equal(6626, reporting.DistrictsReported);
        Assert.Equal(6626, reporting.DistrictsTotal);
        Assert.Equal(7_996_396, reporting.EligibleVotersTotal);
        Assert.Equal(7_996_396, reporting.EligibleVotersCovered);
        Assert.Equal(6_964_673, reporting.TotalVotes);
        Assert.Equal(87.1m, reporting.TurnoutPercent);
        Assert.Equal(100m, reporting.CoveragePercent);
    }

    [Fact]
    public void Tackningsgraden_raknas_pa_rostberattigade_inte_pa_distrikt()
    {
        // Tidig valnatt: få distrikt räknade, och de behöver inte vara representativa.
        var reporting = new ElectionReporting(
            DistrictsReported: 3313,
            DistrictsTotal: 6626,
            EligibleVotersCovered: 1_000_000,
            EligibleVotersTotal: 8_000_000,
            TotalVotes: 900_000,
            TotalVotesPrevious: null,
            TurnoutPercent: null,
            TurnoutPercentPrevious: null);

        // Hälften av distrikten, men bara 12,5 % av de röstberättigade.
        Assert.Equal(12.5m, reporting.CoveragePercent);
    }

    [Fact]
    public void Tackningsgrad_saknas_innan_nagot_raknats()
    {
        var reporting = new ElectionReporting(0, 6626, 0, 0, 0, null, null, null);

        Assert.Null(reporting.CoveragePercent);
    }

    [Fact]
    public void Laser_ovriga_partier_som_klump()
    {
        var other = ParseFixture().OtherParties;

        Assert.Equal(112_779, other.Votes);
        Assert.Equal(1.6m, other.SharePercent);
    }

    [Fact]
    public void Tolkar_tidsstampel_som_svensk_tid()
    {
        // Filens "2026-09-01T12:01:18" saknar offset och avser svensk tid (CEST, +02:00).
        var updatedAt = ParseFixture().Source.UpdatedAt;

        Assert.Equal(TimeSpan.FromHours(2), updatedAt.Offset);
        Assert.Equal(new DateTime(2026, 9, 1, 10, 1, 18, DateTimeKind.Utc), updatedAt.UtcDateTime);
    }

    // --- Trasig indata får inte tolkas som ett tomt eller nollställt resultat ---

    [Fact]
    public void Vagrar_fil_for_annan_valtyp()
    {
        var ex = Assert.Throws<ElectionResultFormatException>(
            () => ParseModified(root => root["valtyp"] = "KF"));

        Assert.Contains("RD", ex.Message);
    }

    [Fact]
    public void Vagrar_okant_rakningstillfalle()
    {
        Assert.Throws<ElectionResultFormatException>(
            () => ParseModified(root => root["rakningstillfalle"] = "gissning"));
    }

    [Fact]
    public void Vagrar_saknat_obligatoriskt_falt()
    {
        Assert.Throws<ElectionResultFormatException>(
            () => ParseModified(root => root.Remove("valdatum")));
    }

    [Fact]
    public void Vagrar_ogiltig_json()
    {
        using var stream = new MemoryStream("{ inte json"u8.ToArray());

        Assert.Throws<ElectionResultFormatException>(
            () => ElectionResultParser.ParseMandateFile(stream, Checksum, Ingested));
    }

    [Fact]
    public void Vagrar_fler_raknade_distrikt_an_totalt()
    {
        Assert.Throws<ElectionResultFormatException>(
            () => ParseModified(root => root["valomrade"]!["antalValdistriktRaknade"] = 9999));
    }

    [Fact]
    public void Kraver_checksumma()
    {
        using var stream = OpenMandateFixture();

        Assert.Throws<ArgumentException>(
            () => ElectionResultParser.ParseMandateFile(stream, "  ", Ingested));
    }

    // --- Fixture-hjälpare ---

    private static ElectionSnapshot ParseFixture()
    {
        using var stream = OpenMandateFixture();
        return ElectionResultParser.ParseMandateFile(stream, Checksum, Ingested);
    }

    /// <summary>Läser fixturen, kör <paramref name="mutate"/> på toppnivån och tolkar resultatet.</summary>
    private static ElectionSnapshot ParseModified(Action<JsonObject> mutate)
    {
        using var source = OpenMandateFixture();
        var root = JsonNode.Parse(source)!.AsObject();
        mutate(root);

        using var modified = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
        return ElectionResultParser.ParseMandateFile(modified, Checksum, Ingested);
    }

    private static Stream OpenMandateFixture()
    {
        var zipPath = Path.Combine(
            FixtureRoot(), "valmyndigheten", "genrep2026", "Genrep_2026_preliminar_00_RD.zip");

        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.Single(e => e.Name.Contains("mandatfordelning", StringComparison.Ordinal)
                                                && e.Name.EndsWith(".json", StringComparison.Ordinal));

        // Kopiera ut posten så att arkivet kan stängas direkt.
        var buffer = new MemoryStream();
        using (var entryStream = entry.Open())
        {
            entryStream.CopyTo(buffer);
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>Letar upp <c>backend/tests/fixtures</c> uppåt från testassemblyn.</summary>
    private static string FixtureRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "fixtures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Hittade ingen fixtures-katalog uppåt från {AppContext.BaseDirectory}.");
    }
}

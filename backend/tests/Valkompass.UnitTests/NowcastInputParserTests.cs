using Valkompass.Application.Election;
using Valkompass.Application.Election.Nowcast;

namespace Valkompass.UnitTests;

/// <summary>
/// Läser den arkiverade genrepsfilen. Förväntningarna nedan är oberoende uträknade ur samma
/// fil, så testerna fångar om parsern börjar tolka formatet annorlunda.
/// </summary>
public class NowcastInputParserTests
{
    // Röstfördelningsfilen är ca 40 MB – parsa en gång och dela mellan testerna.
    private static readonly Lazy<IReadOnlyList<DistrictSnapshot>> Districts = new(() =>
        NowcastInputParser.ParseDistricts(ElectionFixtures.ReadZipEntry("rostfordelning_00_RD.json")));

    private static readonly Lazy<IReadOnlyList<MunicipalitySnapshot>> Municipalities = new(() =>
        NowcastInputParser.ParseMunicipalities(ElectionFixtures.ReadZipEntry("summering_RD.json")));

    [Fact]
    public void Laser_alla_valdistrikt()
    {
        Assert.Equal(6626, Districts.Value.Count);
    }

    [Fact]
    public void Delar_upp_distrikten_efter_jamforbarhet()
    {
        var byStatus = Districts.Value
            .GroupBy(d => d.Comparison)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(5024, byStatus[DistrictComparison.Comparable]);
        Assert.Equal(1567, byStatus[DistrictComparison.NotComparable]);
        Assert.Equal(35, byStatus[DistrictComparison.Aggregated]);
    }

    [Fact]
    public void Uppsamlingsdistrikten_saknar_rostberattigade()
    {
        // 314 uppsamlingsdistrikt: förtids- och brevröster, utan eget väljarunderlag.
        var withoutEligible = Districts.Value.Where(d => d.EligibleVoters is null).ToList();

        Assert.Equal(314, withoutEligible.Count);
        Assert.All(withoutEligible, d => Assert.Equal(DistrictComparison.NotComparable, d.Comparison));
    }

    [Fact]
    public void Laser_ett_distrikt_med_alla_falt()
    {
        var d = Assert.Single(Districts.Value, x => x.Code == "01800101");

        Assert.Equal("0180", d.MunicipalityCode);
        Assert.Equal("01", d.CountyCode);
        Assert.True(d.IsReported);
        Assert.Equal(DistrictComparison.Comparable, d.Comparison);
        Assert.Equal(1264, d.EligibleVoters);
        Assert.Equal(1064, d.Votes);
        Assert.Equal(1087, d.VotesPrevious);

        var m = Assert.Single(d.Parties, p => p.PartyCode == "M");
        Assert.Equal(208, m.Votes);
        Assert.Equal(207, m.VotesPrevious);
    }

    [Fact]
    public void Alla_distrikt_har_de_atta_riksdagspartierna()
    {
        Assert.All(Districts.Value, d => Assert.Equal(8, d.Parties.Count));
    }

    [Fact]
    public void Distriktsnivan_tacker_bara_en_dryg_tredjedel_av_2022_ars_roster()
    {
        // Viktigt för modellen: 2022-data saknas för de icke jämförbara distrikten, som
        // rymmer drygt en femtedel av rösterna. Volymen måste därför modelleras per kommun.
        var withPrevious = Districts.Value.Where(d => d.VotesPrevious is not null).ToList();

        Assert.Equal(5059, withPrevious.Count);
        Assert.Equal(5_085_094, withPrevious.Sum(d => d.VotesPrevious!.Value));
    }

    [Fact]
    public void Bara_jamforbara_rapporterade_distrikt_ger_swing()
    {
        // I den färdigräknade genrepsfilen har alla distrikt rapporterat, så det som återstår
        // är jämförbarheten.
        Assert.Equal(5059, Districts.Value.Count(d => d.CanMeasureSwing));
    }

    // --- Kommunnivå ---

    [Fact]
    public void Laser_alla_kommuner()
    {
        Assert.Equal(290, Municipalities.Value.Count);
        Assert.All(Municipalities.Value, k => Assert.NotNull(k.VotesPrevious));
    }

    [Fact]
    public void Kommunernas_2022_summa_tacker_nastan_hela_riket()
    {
        // Skillnaden mot rikets 6 547 801 är röster som inte hör till någon kommun.
        // Att summan ligger nära totalen är det som gör den användbar som fast baseline.
        Assert.Equal(6_477_970, Municipalities.Value.Sum(k => k.VotesPrevious!.Value));
    }

    [Fact]
    public void Kommunernas_rostberattigade_summerar_till_riket()
    {
        Assert.Equal(7_996_396, Municipalities.Value.Sum(k => k.EligibleVoters));
        Assert.Equal(6_877_640, Municipalities.Value.Sum(k => k.Votes));
    }

    [Fact]
    public void Kommunerna_vet_hur_manga_distrikt_som_ska_raknas()
    {
        Assert.Equal(6626, Municipalities.Value.Sum(k => k.DistrictsTotal));
        Assert.Equal(6626, Municipalities.Value.Sum(k => k.DistrictsReported));
    }

    [Fact]
    public void Tom_rapporteringstid_ar_inte_ett_raknat_distrikt()
    {
        // Genrepsfilen är färdigräknad och har en tidsstämpel överallt, så det här fallet
        // syntes först i skarp data: en delvis räknad fil listar alla 6626 distrikten hela
        // kvällen och märker de oräknade med tom sträng, inte null. Räknas de som
        // rapporterade ser modellen ett färdigräknat val och avstår från att prognosticera.
        var json = """
        { "valdistrikt": [
          { "valdistriktskod": "018001", "kommunkod": "0180", "lankod": "01",
            "statusJamforelse": "Ej jämförbart", "antalRostberattigade": null,
            "rapporteringsTid": "",
            "rostfordelning": { "rosterPaverkaMandat": { "antalRoster": 0, "partiRoster": [] } } },
          { "valdistriktskod": "01800405", "kommunkod": "0180", "lankod": "01",
            "statusJamforelse": "Kan jämföras", "antalRostberattigade": 1264,
            "rapporteringsTid": "2026-09-13T21:34:39",
            "rostfordelning": { "rosterPaverkaMandat": { "antalRoster": 1270, "partiRoster": [] } } }
        ] }
        """u8.ToArray();

        var districts = NowcastInputParser.ParseDistricts(json);

        Assert.False(Assert.Single(districts, d => d.Code == "018001").IsReported);
        Assert.True(Assert.Single(districts, d => d.Code == "01800405").IsReported);
    }

    [Fact]
    public void Orapporterade_distrikt_slar_inte_ihop_sig_med_nasta()
    {
        // Valmyndigheten skriver "rostfordelning": null för distrikt som inte rapporterat.
        // Läses null som ett objekt tappar läsaren nivån och drar in nästa distrikts fält i
        // det föregående – tyst, utan fel. På valnatten blev 6626 distrikt 979 hopslagna
        // poster, och modellen räknade på dem som om de vore verkliga valdistrikt.
        var json = """
        { "valdistrikt": [
          { "valdistriktskod": "018001", "kommunkod": "0180", "lankod": "01",
            "rapporteringsTid": "", "statusJamforelse": "Ej jämförbart",
            "antalRostberattigade": null, "rostfordelning": null },
          { "valdistriktskod": "01800405", "kommunkod": "0180", "lankod": "01",
            "rapporteringsTid": "2026-09-13T21:34:39", "statusJamforelse": "Kan jämföras",
            "antalRostberattigade": 1264,
            "rostfordelning": { "rosterPaverkaMandat": { "antalRoster": 1270,
              "partiRoster": [ { "partiforkortning": "S", "antalRoster": 400 } ] } } },
          { "valdistriktskod": "01800406", "kommunkod": "0180", "lankod": "01",
            "rapporteringsTid": "", "statusJamforelse": "Kan jämföras",
            "antalRostberattigade": 900, "rostfordelning": null }
        ] }
        """u8.ToArray();

        var districts = NowcastInputParser.ParseDistricts(json);

        Assert.Equal(3, districts.Count);
        Assert.Equal(
            new[] { "018001", "01800405", "01800406" },
            districts.Select(d => d.Code).ToArray());

        var counted = Assert.Single(districts, d => d.IsReported);
        Assert.Equal("01800405", counted.Code);
        Assert.Equal(1270, counted.Votes);
        Assert.Equal(400, Assert.Single(counted.Parties).Votes);

        // Ett orapporterat distrikt har inga röster – men vet fortfarande vilket det är.
        var pending = Assert.Single(districts, d => d.Code == "01800406");
        Assert.False(pending.IsReported);
        Assert.Equal(0, pending.Votes);
        Assert.Empty(pending.Parties);
        Assert.Equal(900, pending.EligibleVoters);
    }

    [Fact]
    public void Kommuner_utan_rostfordelning_slar_inte_ihop_sig_med_nasta()
    {
        // Samma sak i summeringsfilen: kommuner som inte börjat räkna har null.
        var json = """
        { "kommuner": [
          { "kommunkod": "0115", "lankod": "01", "antalRostberattigade": 25000,
            "antalValdistriktRaknade": 0, "antalValdistriktSomSkaRaknas": 18,
            "rostfordelning": null },
          { "kommunkod": "0180", "lankod": "01", "antalRostberattigade": 700000,
            "antalValdistriktRaknade": 12, "antalValdistriktSomSkaRaknas": 400,
            "rostfordelning": { "rosterPaverkaMandat": { "antalRoster": 9000,
              "antalRosterForegaendeVal": 500000, "partiRoster": [] } } }
        ] }
        """u8.ToArray();

        var municipalities = NowcastInputParser.ParseMunicipalities(json);

        Assert.Equal(2, municipalities.Count);
        Assert.Equal(new[] { "0115", "0180" }, municipalities.Select(m => m.Code).ToArray());
        Assert.Equal(18, municipalities[0].DistrictsTotal);
        Assert.Null(municipalities[0].VotesPrevious);
        Assert.Equal(500_000, municipalities[1].VotesPrevious);
    }

    [Fact]
    public void Vagrar_fil_utan_valdistrikt()
    {
        var json = """{ "valtyp": "RD" }"""u8.ToArray();

        Assert.Throws<ElectionResultFormatException>(() => NowcastInputParser.ParseDistricts(json));
    }
}

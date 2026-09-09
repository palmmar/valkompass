using Valkompass.Application.Election;
using Valkompass.Domain.Enums;

namespace Valkompass.UnitTests;

public class ElectionIndexTests
{
    // Utdrag ur den arkiverade genrep-indexfilen.
    private const string Sample = """
        d1035d820b9a2a78ead3b9f0870fd4a6  ./p/kf/Genrep_2026_preliminar_0114_KF.zip
        3e6407d8abce4a7856a396cd61e02724  ./p/rd/Genrep_2026_preliminar_00_RD.zip
        0f17c797375cfff9f092200377fa090e  ./p/rf/Genrep_2026_preliminar_01_RF.zip
        f4d220f18d61c1aded3b7ac4aac50949  ./s/rd/Genrep_2026_slutlig_00_RD.zip
        """;

    [Fact]
    public void Laser_checksumma_och_sokvag()
    {
        var entries = ElectionIndex.Parse(Sample);

        Assert.Equal(4, entries.Count);
        Assert.Equal("d1035d820b9a2a78ead3b9f0870fd4a6", entries[0].Checksum);
        Assert.Equal("./p/kf/Genrep_2026_preliminar_0114_KF.zip", entries[0].RelativePath);
        Assert.Equal("Genrep_2026_preliminar_0114_KF.zip", entries[0].FileName);
    }

    [Fact]
    public void Hittar_riksdagens_preliminara_fil()
    {
        var entry = ElectionIndex.FindParliamentaryResult(
            ElectionIndex.Parse(Sample), CountingStage.Preliminary);

        Assert.NotNull(entry);
        Assert.Equal("Genrep_2026_preliminar_00_RD.zip", entry.FileName);
        Assert.Equal("3e6407d8abce4a7856a396cd61e02724", entry.Checksum);
    }

    [Fact]
    public void Hittar_riksdagens_slutliga_fil()
    {
        var entry = ElectionIndex.FindParliamentaryResult(
            ElectionIndex.Parse(Sample), CountingStage.Final);

        Assert.NotNull(entry);
        Assert.Equal("Genrep_2026_slutlig_00_RD.zip", entry.FileName);
    }

    [Fact]
    public void Valjer_inte_region_eller_kommunfiler()
    {
        // 0114_KF och 01_RF slutar också på siffror + valtyp; bara _00_RD är riksdagen.
        var entry = ElectionIndex.FindParliamentaryResult(
            ElectionIndex.Parse(Sample), CountingStage.Preliminary);

        Assert.DoesNotContain("KF", entry!.FileName, StringComparison.Ordinal);
        Assert.DoesNotContain("RF", entry.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Fungerar_for_skarpa_filnamn()
    {
        // Produktionsfilerna heter Val_2026_… i stället för Genrep_2026_….
        var entries = ElectionIndex.Parse(
            "abc123  ./p/rd/Val_2026_preliminar_00_RD.zip");

        var entry = ElectionIndex.FindParliamentaryResult(entries, CountingStage.Preliminary);

        Assert.Equal("Val_2026_preliminar_00_RD.zip", entry!.FileName);
    }

    [Fact]
    public void Tomt_produktionsindex_ger_inga_poster()
    {
        // Före valnatten svarar val2026/index.md5 med md5 av tom indata och sökvägen "-".
        var entries = ElectionIndex.Parse("d41d8cd98f00b204e9800998ecf8427e  -\n");

        Assert.Empty(entries);
        Assert.Null(ElectionIndex.FindParliamentaryResult(entries, CountingStage.Preliminary));
    }

    [Fact]
    public void Saknad_riksdagsfil_ger_null_i_stallet_for_fel()
    {
        // Rösträkningen kan ha börjat publiceras för kommuner innan riksdagsfilen finns.
        var entries = ElectionIndex.Parse("abc  ./p/kf/Val_2026_preliminar_0114_KF.zip");

        Assert.Null(ElectionIndex.FindParliamentaryResult(entries, CountingStage.Preliminary));
    }

    [Fact]
    public void Ignorerar_tomma_och_trasiga_rader()
    {
        var entries = ElectionIndex.Parse("\n\nskräp\n   \nabc  ./p/rd/Val_2026_preliminar_00_RD.zip\n");

        Assert.Single(entries);
    }

    [Fact]
    public void Hanterar_crlf()
    {
        var entries = ElectionIndex.Parse(
            "abc  ./p/rd/Val_2026_preliminar_00_RD.zip\r\ndef  ./s/rd/Val_2026_slutlig_00_RD.zip\r\n");

        Assert.Equal(2, entries.Count);
        Assert.Equal("./p/rd/Val_2026_preliminar_00_RD.zip", entries[0].RelativePath);
    }
}

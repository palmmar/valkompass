using Valkompass.Application.Election;

namespace Valkompass.UnitTests;

/// <summary>
/// Hela valveckan genomspelad. Poängen med schemat är att ingen ska behöva slå på importen
/// manuellt, och framför allt inte starta om servern mitt under rösträkningen för att göra det.
/// </summary>
public class ElectionImportScheduleTests
{
    private static readonly DateTimeOffset PollsClose =
        new(2026, 9, 13, 20, 0, 0, TimeSpan.FromHours(2));

    private static readonly ElectionTimeline Timeline = new()
    {
        PollsCloseAt = PollsClose,
        ImportStartsBefore = TimeSpan.FromHours(1),
        ImportStopsAfter = TimeSpan.FromDays(21),
    };

    [Fact]
    public void Vantar_dagarna_fore_valet()
    {
        var decision = Decide(PollsClose.AddDays(-4));

        Assert.Equal(ImportAction.Wait, decision.Action);
        // Vilar länge, men kollar ändå av med jämna mellanrum.
        Assert.Equal(ElectionImportSchedule.MaxIdleDelay, decision.Delay);
    }

    [Fact]
    public void Startar_av_sig_sjalv_innan_vallokalerna_stanger()
    {
        // Ingen flagga att komma ihåg, ingen omstart som behövs.
        var decision = Decide(PollsClose.AddMinutes(-30));

        Assert.Equal(ImportAction.Import, decision.Action);
    }

    [Fact]
    public void Vantar_precis_innan_fonstret_oppnar()
    {
        var decision = Decide(PollsClose.AddHours(-1).AddSeconds(-30));

        Assert.Equal(ImportAction.Wait, decision.Action);
        // Sover bara fram till öppningen, inte längre.
        Assert.Equal(TimeSpan.FromSeconds(30), decision.Delay);
    }

    [Fact]
    public void Importerar_under_valnatten()
    {
        var decision = Decide(PollsClose.AddHours(3));

        Assert.Equal(ImportAction.Import, decision.Action);
        Assert.Equal(TimeSpan.FromSeconds(30), decision.Delay);
    }

    [Fact]
    public void Importerar_under_onsdagens_uppsamlingsrakning()
    {
        // Sent inkomna röster räknas på onsdagen, tre dagar efter valdagen.
        var decision = Decide(PollsClose.AddDays(3));

        Assert.Equal(ImportAction.Import, decision.Action);
    }

    [Fact]
    public void Importerar_under_slutliga_rakningen_veckan_efter()
    {
        var decision = Decide(PollsClose.AddDays(9));

        Assert.Equal(ImportAction.Import, decision.Action);
    }

    [Fact]
    public void Slutar_nar_fonstret_passerats()
    {
        var decision = Decide(PollsClose.AddDays(22));

        Assert.Equal(ImportAction.Finished, decision.Action);
    }

    // --- Nödutgången ---

    [Fact]
    public void Avstangd_import_pausar_i_stallet_for_att_hamta()
    {
        var decision = Decide(PollsClose.AddHours(3), enabled: false);

        Assert.Equal(ImportAction.Paused, decision.Action);
    }

    [Fact]
    public void Pausad_import_fortsatter_kolla_sa_att_paslagning_hittas()
    {
        // Annars hade en påslagning krävt omstart – precis det vi vill undvika.
        var decision = Decide(PollsClose.AddHours(3), enabled: false);

        Assert.Equal(TimeSpan.FromSeconds(30), decision.Delay);
    }

    [Fact]
    public void Passerat_fonster_vinner_over_nodutgangen()
    {
        // Efter slutlig räkning finns inget att hämta, oavsett flaggans värde.
        var decision = Decide(PollsClose.AddDays(22), enabled: false);

        Assert.Equal(ImportAction.Finished, decision.Action);
    }

    private static ImportDecision Decide(DateTimeOffset now, bool enabled = true) =>
        ElectionImportSchedule.Decide(
            Timeline,
            new ElectionImport { Enabled = enabled, PollInterval = TimeSpan.FromSeconds(30) },
            now);
}

using Valkompass.Application.Election;
using Valkompass.Domain.Enums;

namespace Valkompass.UnitTests;

/// <summary>
/// Hela valkvällen genomspelad utan att vänta på den. Klockan avgör bara om vallokalerna
/// stängt; därefter styr rösträkningens faktiska status.
/// </summary>
public class ElectionPhaseCalculatorTests
{
    private static readonly DateTimeOffset PollsClose =
        new(2026, 9, 13, 20, 0, 0, TimeSpan.FromHours(2));

    private static readonly ElectionTimeline Timeline = new()
    {
        PollsCloseAt = PollsClose,
        StaleAfter = TimeSpan.FromMinutes(10),
    };

    [Fact]
    public void Fore_valdagen_ar_preElection()
    {
        Assert.Equal(ElectionPhase.PreElection, Resolve(null, PollsClose.AddDays(-2)));
    }

    [Fact]
    public void Valdagen_fore_stangning_ar_electionDay()
    {
        // Söndag morgon: vallokalerna är öppna.
        Assert.Equal(ElectionPhase.ElectionDay, Resolve(null, PollsClose.AddHours(-12)));
    }

    [Fact]
    public void Strax_fore_midnatt_dagen_innan_ar_fortfarande_preElection()
    {
        // Gränsfallet som gjorde att nedräkningen nollställdes ett dygn för tidigt:
        // lördag 23:00 är fortfarande före valdagen, inte "valet är genomfört".
        Assert.Equal(ElectionPhase.PreElection, Resolve(null, PollsClose.AddHours(-21)));
    }

    [Fact]
    public void Efter_stangning_utan_resultat_ar_waitingForResults()
    {
        Assert.Equal(ElectionPhase.WaitingForResults, Resolve(null, PollsClose.AddMinutes(5)));
    }

    [Fact]
    public void Snapshot_utan_raknade_distrikt_ar_waitingForResults()
    {
        // Filen kan finnas innan något distrikt rapporterat.
        var snapshot = Snapshot(reported: 0);

        Assert.Equal(ElectionPhase.WaitingForResults, Resolve(snapshot, PollsClose.AddMinutes(20)));
    }

    [Fact]
    public void Pagaende_preliminar_rakning_ar_live()
    {
        Assert.Equal(ElectionPhase.Live, Resolve(Snapshot(reported: 1200), PollsClose.AddHours(1)));
    }

    [Fact]
    public void Fardigraknad_preliminar_rakning_ar_preliminaryPaused()
    {
        // Alla distrikt räknade betyder inte klart: onsdagens uppsamlingsräkning återstår.
        var snapshot = Snapshot(reported: 6626);

        Assert.Equal(ElectionPhase.PreliminaryPaused, Resolve(snapshot, PollsClose.AddHours(6)));
    }

    [Fact]
    public void Slutlig_rakning_som_pagar_ar_finalCounting()
    {
        var snapshot = Snapshot(reported: 3000, stage: CountingStage.Final);

        Assert.Equal(ElectionPhase.FinalCounting, Resolve(snapshot, PollsClose.AddDays(2)));
    }

    [Fact]
    public void Fardig_slutlig_rakning_ar_finished()
    {
        var snapshot = Snapshot(reported: 6626, stage: CountingStage.Final);

        Assert.Equal(ElectionPhase.Finished, Resolve(snapshot, PollsClose.AddDays(4)));
    }

    // --- Stale ---

    [Fact]
    public void Fardsk_snapshot_ar_inte_stale()
    {
        var now = PollsClose.AddHours(2);
        var snapshot = Snapshot(reported: 1200, updatedAt: now.AddMinutes(-2));

        Assert.False(ElectionPhaseCalculator.IsStale(Timeline, snapshot, now));
    }

    [Fact]
    public void Gammal_snapshot_ar_stale()
    {
        var now = PollsClose.AddHours(2);
        var snapshot = Snapshot(reported: 1200, updatedAt: now.AddMinutes(-30));

        Assert.True(ElectionPhaseCalculator.IsStale(Timeline, snapshot, now));
    }

    [Fact]
    public void Stale_paverkar_inte_fasen()
    {
        // En fördröjd källa ändrar inte var i rösträkningen vi är – bara hur färska
        // siffrorna är. Frontend visar senaste giltiga snapshot märkt som fördröjd.
        var now = PollsClose.AddHours(2);
        var snapshot = Snapshot(reported: 1200, updatedAt: now.AddHours(-1));

        Assert.True(ElectionPhaseCalculator.IsStale(Timeline, snapshot, now));
        Assert.Equal(ElectionPhase.Live, Resolve(snapshot, now));
    }

    [Fact]
    public void Utan_snapshot_finns_inget_att_kalla_stale()
    {
        Assert.False(ElectionPhaseCalculator.IsStale(Timeline, null, PollsClose.AddHours(2)));
    }

    // --- Hjälpare ---

    private static ElectionPhase Resolve(ElectionSnapshot? snapshot, DateTimeOffset now) =>
        ElectionPhaseCalculator.Resolve(Timeline, snapshot, now);

    private static ElectionSnapshot Snapshot(
        int reported,
        CountingStage stage = CountingStage.Preliminary,
        DateTimeOffset? updatedAt = null) =>
        new(
            Source: new ElectionSnapshotSource(
                ElectionDate: new DateOnly(2026, 9, 13),
                PreviousElectionDate: new DateOnly(2022, 9, 11),
                Stage: stage,
                IsTest: false,
                UpdatedAt: updatedAt ?? PollsClose,
                IngestedAt: updatedAt ?? PollsClose,
                Checksum: "abc",
                UpdateCount: 1),
            Reporting: new ElectionReporting(
                DistrictsReported: reported,
                DistrictsTotal: 6626,
                EligibleVotersCovered: reported * 1000,
                EligibleVotersTotal: 7_996_396,
                TotalVotes: reported * 800,
                TotalVotesPrevious: null,
                TurnoutPercent: null,
                TurnoutPercentPrevious: null),
            Results: [],
            Mandates: [],
            OtherParties: new OtherPartiesResult(0, 0m, null, null),
            ThresholdPercent: 4m,
            ConstituencyThresholdPercent: 12m);
}

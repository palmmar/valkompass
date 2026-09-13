using Microsoft.Extensions.DependencyInjection;
using Valkompass.Application.Contracts;
using Valkompass.Application.Election;
using Valkompass.Domain.Enums;

namespace Valkompass.IntegrationTests;

/// <summary>
/// Sparvägen mot riktig Postgres. Enhetstesterna kör mot en fejkad store och kan därför inte
/// se vad Npgsql accepterar – och det är just där valvakan föll: Valmyndighetens tidsstämplar
/// är svensk tid, och <c>timestamptz</c> tar bara UTC. Importen hämtade, verifierade och
/// tolkade filen men kraschade på sista steget, så sidan stod kvar på "väntar på resultat".
/// </summary>
[Collection(ApiCollection.Name)]
public class ElectionSnapshotStoreTests(ApiFactory factory)
{
    /// <summary>Svensk sommartid, precis som i Valmyndighetens filer.</summary>
    private static readonly TimeSpan SwedishOffset = TimeSpan.FromHours(2);

    [Fact]
    public async Task Sparar_snapshot_med_svensk_tidsstampel()
    {
        var updatedAt = new DateTimeOffset(2026, 9, 13, 20, 38, 57, SwedishOffset);
        var snapshot = Snapshot(updatedAt, checksum: Guid.NewGuid().ToString("n"));

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IElectionSnapshotStore>();

        Assert.True(await store.SaveAsync(snapshot, forecast: null));

        var stored = await store.GetLatestAsync(CountingStage.Preliminary, includeTestData: false);

        Assert.NotNull(stored);
        // Samma ögonblick tillbaka, oavsett vilken offset som skrevs.
        Assert.Equal(updatedAt.UtcDateTime, stored!.Result.Source.UpdatedAt.UtcDateTime);
        Assert.Equal(3, stored.Result.Reporting.DistrictsReported);
    }

    [Fact]
    public async Task Samma_checksumma_sparas_bara_en_gang()
    {
        var checksum = Guid.NewGuid().ToString("n");
        var snapshot = Snapshot(new DateTimeOffset(2026, 9, 13, 21, 0, 0, SwedishOffset), checksum);

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IElectionSnapshotStore>();

        Assert.True(await store.SaveAsync(snapshot, forecast: null));
        Assert.False(await store.SaveAsync(snapshot, forecast: null));
    }

    /// <summary>
    /// Ett tidigt valnattsläge: några räknade distrikt, inga mandat fördelade än.
    /// </summary>
    private static ElectionSnapshot Snapshot(DateTimeOffset updatedAt, string checksum) => new(
        Source: new ElectionSnapshotSource(
            ElectionDate: new DateOnly(2026, 9, 13),
            PreviousElectionDate: new DateOnly(2022, 9, 11),
            Stage: CountingStage.Preliminary,
            IsTest: false,
            UpdatedAt: updatedAt,
            IngestedAt: updatedAt.AddSeconds(20),
            Checksum: checksum,
            UpdateCount: 3),
        Reporting: new ElectionReporting(
            DistrictsReported: 3,
            DistrictsTotal: 6626,
            EligibleVotersCovered: 1231,
            EligibleVotersTotal: 8_051_238,
            TotalVotes: 1046,
            TotalVotesPrevious: 6_547_801,
            TurnoutPercent: 85.0m,
            TurnoutPercentPrevious: 84.2m),
        Results: [new PartyResult("S", "Arbetarepartiet-Socialdemokraterna", 5, "#E8112d", 251, 24.0m, null, null, null)],
        Mandates: [],
        OtherParties: new OtherPartiesResult(2, 0.2m, null, null),
        ThresholdPercent: 4m,
        ConstituencyThresholdPercent: 12m);
}

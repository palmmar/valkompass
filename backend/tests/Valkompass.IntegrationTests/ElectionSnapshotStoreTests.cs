using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Valkompass.Application.Contracts;
using Valkompass.Application.Election;
using Valkompass.Domain.Enums;
using Valkompass.Infrastructure.Persistence;

namespace Valkompass.IntegrationTests;

/// <summary>
/// Sparvägen mot riktig Postgres. Enhetstesterna kör importen mot en fejkad store och kan
/// därför inte se vad Npgsql accepterar – och det är just där valvakan föll: Valmyndighetens
/// tidsstämplar är svensk tid, och <c>timestamptz</c> tar bara UTC. Importen hämtade,
/// verifierade och tolkade filen men kraschade på sista steget, så sidan stod kvar på
/// "väntar på resultat".
/// </summary>
/// <remarks>
/// Varje test tömmer tabellen efteråt. Resten av integrationstesterna delar databas och utgår
/// från att den är tom – <see cref="ElectionLiveApiTests"/> beskriver läget före rösträkningen
/// och skulle annars se våra rader.
/// </remarks>
[Collection(ApiCollection.Name)]
public class ElectionSnapshotStoreTests(ApiFactory factory)
{
    /// <summary>Svensk sommartid, precis som i Valmyndighetens filer.</summary>
    private static readonly TimeSpan SwedishOffset = TimeSpan.FromHours(2);

    [Fact]
    public async Task Sparar_snapshot_med_svensk_tidsstampel()
    {
        var updatedAt = new DateTimeOffset(2026, 9, 13, 20, 38, 57, SwedishOffset);

        await WithEmptyTableAsync(async (store, _) =>
        {
            Assert.True(await store.SaveAsync(Snapshot(updatedAt), forecast: null));

            var stored = await store.GetLatestAsync(CountingStage.Preliminary, includeTestData: false);

            Assert.NotNull(stored);
            // Samma ögonblick tillbaka, oavsett vilken offset som skrevs.
            Assert.Equal(updatedAt.UtcDateTime, stored!.Result.Source.UpdatedAt.UtcDateTime);
            Assert.Equal(3, stored.Result.Reporting.DistrictsReported);
            Assert.Empty(stored.Result.Mandates);
        });
    }

    [Fact]
    public async Task Samma_checksumma_sparas_bara_en_gang()
    {
        var snapshot = Snapshot(new DateTimeOffset(2026, 9, 13, 21, 0, 0, SwedishOffset));

        await WithEmptyTableAsync(async (store, db) =>
        {
            Assert.True(await store.SaveAsync(snapshot, forecast: null));
            Assert.False(await store.SaveAsync(snapshot, forecast: null));

            Assert.Equal(1, await db.ElectionSnapshots.CountAsync());
        });
    }

    /// <summary>
    /// Kör testet mot en tom <c>election_snapshots</c> och lämnar den tom igen, även när
    /// testet fallerar.
    /// </summary>
    private async Task WithEmptyTableAsync(Func<IElectionSnapshotStore, AppDbContext, Task> test)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IElectionSnapshotStore>();

        await db.ElectionSnapshots.ExecuteDeleteAsync();
        try
        {
            await test(store, db);
        }
        finally
        {
            await db.ElectionSnapshots.ExecuteDeleteAsync();
        }
    }

    /// <summary>Ett tidigt valnattsläge: några räknade distrikt, inga mandat fördelade än.</summary>
    private static ElectionSnapshot Snapshot(DateTimeOffset updatedAt) => new(
        Source: new ElectionSnapshotSource(
            ElectionDate: new DateOnly(2026, 9, 13),
            PreviousElectionDate: new DateOnly(2022, 9, 11),
            Stage: CountingStage.Preliminary,
            IsTest: false,
            UpdatedAt: updatedAt,
            IngestedAt: updatedAt.AddSeconds(20),
            Checksum: Guid.NewGuid().ToString("n"),
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

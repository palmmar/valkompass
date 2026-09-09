using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Valkompass.Application.Contracts;
using Valkompass.Application.Dtos;
using Valkompass.Application.Election;
using Valkompass.Domain.Enums;

namespace Valkompass.Infrastructure.Services;

/// <summary>
/// Sätter ihop svaret för <c>GET /api/election/live</c>.
/// </summary>
/// <remarks>
/// Snapshoten cachas några sekunder. Importen mot Valmyndigheten är redan frikopplad från
/// antalet läsare, men utan cache hade varje pollande klient blivit en databasfråga. Cachen är
/// kort nog att kännas direkt under en valnatt.
/// </remarks>
public class ElectionLiveService(
    IElectionSnapshotStore store,
    IMemoryCache cache,
    IOptions<ElectionTimeline> timeline,
    IOptions<ElectionImport> import,
    TimeProvider time)
    : IElectionLiveService
{
    private const string CacheKey = "election:live:snapshot";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    private readonly ElectionTimeline _timeline = timeline.Value;
    private readonly ElectionImport _import = import.Value;

    public async Task<ElectionLiveResponse> GetLiveAsync(CancellationToken ct = default)
    {
        var snapshot = await GetSnapshotAsync(ct);
        var now = time.GetUtcNow();

        var phase = ElectionPhaseCalculator.Resolve(_timeline, snapshot, now);
        var stale = ElectionPhaseCalculator.IsStale(_timeline, snapshot, now);

        if (snapshot is null)
        {
            // Före rösträkningen finns ingen källa att beskriva. Fasen är fortfarande giltig,
            // så sidan kan visa nedräkning eller "väntar på resultat".
            return new ElectionLiveResponse(
                Phase: phase,
                Source: null,
                Reporting: null,
                Results: [],
                OfficialMandates: [],
                Thresholds: new ElectionThresholdsDto(4m, 12m));
        }

        return new ElectionLiveResponse(
            Phase: phase,
            Source: new ElectionSourceDto(
                UpdatedAt: snapshot.Source.UpdatedAt,
                IngestedAt: snapshot.Source.IngestedAt,
                Stale: stale,
                IsTest: snapshot.Source.IsTest,
                Stage: snapshot.Source.Stage == CountingStage.Final ? "final" : "preliminary"),
            Reporting: new ElectionReportingDto(
                DistrictsReported: snapshot.Reporting.DistrictsReported,
                DistrictsTotal: snapshot.Reporting.DistrictsTotal,
                EligibleVotersCovered: snapshot.Reporting.EligibleVotersCovered,
                EligibleVotersTotal: snapshot.Reporting.EligibleVotersTotal,
                CoveragePercent: snapshot.Reporting.CoveragePercent,
                TotalVotes: snapshot.Reporting.TotalVotes,
                TurnoutPercent: snapshot.Reporting.TurnoutPercent,
                TurnoutPercentPrevious: snapshot.Reporting.TurnoutPercentPrevious),
            Results: [.. snapshot.Results
                .OrderByDescending(r => r.SharePercent)
                .ThenBy(r => r.DisplayOrder)
                .Select(r => new ElectionPartyResultDto(
                    PartyCode: r.Code,
                    Name: r.Name,
                    DisplayOrder: r.DisplayOrder,
                    Votes: r.Votes,
                    SharePercent: r.SharePercent,
                    SharePreviousPercent: r.SharePreviousPercent,
                    ShareChangePoints: r.ShareChangePoints))],
            OfficialMandates: [.. snapshot.Mandates.Select(m => new ElectionMandateDto(
                PartyCode: m.Code,
                Mandates: m.Total,
                FixedMandates: m.Fixed,
                LevellingMandates: m.Levelling,
                MandatesPrevious: m.TotalPrevious,
                Change: m.Change))],
            Thresholds: new ElectionThresholdsDto(
                snapshot.ThresholdPercent, snapshot.ConstituencyThresholdPercent));
    }

    private async Task<ElectionSnapshot?> GetSnapshotAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out ElectionSnapshot? cached))
        {
            return cached;
        }

        // Slutlig räkning ersätter den preliminära så fort den finns.
        var snapshot = await store.GetLatestAsync(CountingStage.Final, _import.AllowTestData, ct)
            ?? await store.GetLatestAsync(CountingStage.Preliminary, _import.AllowTestData, ct);

        cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }
}

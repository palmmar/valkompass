using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Valkompass.Application.Contracts;
using Valkompass.Application.Dtos;
using Valkompass.Application.Election;
using Valkompass.Application.Election.Nowcast;
using Valkompass.Domain.Enums;
using Valkompass.Infrastructure.Persistence;

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
    AppDbContext db,
    IElectionSnapshotStore store,
    IMemoryCache cache,
    IOptions<ElectionTimeline> timeline,
    IOptions<ElectionImport> import,
    TimeProvider time)
    : IElectionLiveService
{
    private const string CacheKey = "election:live:snapshot";
    private const string ColorCacheKey = "election:live:party-colors";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    // Partifärgerna ändras bara när någon redigerar ett parti i admin, så de behöver inte
    // hämtas lika ofta som siffrorna.
    private static readonly TimeSpan ColorCacheDuration = TimeSpan.FromMinutes(5);

    private readonly ElectionTimeline _timeline = timeline.Value;
    private readonly ElectionImport _import = import.Value;

    public async Task<ElectionLiveResponse> GetLiveAsync(CancellationToken ct = default)
    {
        var stored = await GetSnapshotAsync(ct);
        var snapshot = stored?.Result;
        var colors = await GetPartyColorsAsync(ct);
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
                    Color: ResolveColor(colors, r),
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
                snapshot.ThresholdPercent, snapshot.ConstituencyThresholdPercent),
            Forecast: ToDto(stored?.Forecast));
    }

    /// <summary>
    /// Vår egen partipalett, samma som barometern och resultatsidan visar. Valmyndighetens
    /// <c>fargkod</c> finns i snapshoten och används som reserv, men den ger fyra snarlika
    /// blå toner – på ett stapeldiagram går partierna då inte att skilja åt.
    /// </summary>
    private static string? ResolveColor(IReadOnlyDictionary<string, string?> colors, PartyResult result)
    {
        if (colors.TryGetValue(result.Code, out var color) && !string.IsNullOrWhiteSpace(color))
        {
            return color;
        }

        return string.IsNullOrWhiteSpace(result.ColorHex) ? null : result.ColorHex;
    }

    private async Task<IReadOnlyDictionary<string, string?>> GetPartyColorsAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(ColorCacheKey, out IReadOnlyDictionary<string, string?>? cached) && cached is not null)
        {
            return cached;
        }

        var colors = await db.Parties.AsNoTracking().ToDictionaryAsync(p => p.Code, p => p.Color, ct);

        cache.Set(ColorCacheKey, (IReadOnlyDictionary<string, string?>)colors, ColorCacheDuration);
        return colors;
    }

    private async Task<StoredElectionSnapshot?> GetSnapshotAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out StoredElectionSnapshot? cached))
        {
            return cached;
        }

        // Slutlig räkning ersätter den preliminära så fort den finns.
        var snapshot = await store.GetLatestAsync(CountingStage.Final, _import.AllowTestData, ct)
            ?? await store.GetLatestAsync(CountingStage.Preliminary, _import.AllowTestData, ct);

        cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    /// <summary>
    /// Översätter prognosen till API-kontraktet. Returnerar null när modellen avstått, så att
    /// sidan visar räknat resultat utan prognos i stället för ett tomt löfte.
    /// </summary>
    private static ElectionForecastDto? ToDto(NowcastResult? forecast)
    {
        if (forecast is not { Available: true, Metadata: not null })
        {
            return null;
        }

        return new ElectionForecastDto(
            Parties: [.. forecast.Parties.Select(p => new ElectionForecastPartyDto(
                PartyCode: p.PartyCode,
                ForecastShare: p.ForecastShare,
                Lower90: p.Lower90,
                Upper90: p.Upper90,
                ProbabilityAboveThreshold: p.ProbabilityAboveThreshold))],
            Confidence: forecast.Metadata.Confidence switch
            {
                NowcastConfidence.VeryHigh => "veryHigh",
                NowcastConfidence.High => "high",
                NowcastConfidence.Medium => "medium",
                _ => "low",
            },
            TypicalUncertaintyPoints: forecast.Metadata.TypicalUncertaintyPoints,
            CoveragePercent: forecast.Metadata.CoveragePercent,
            RegionalSkewPercent: forecast.Metadata.RegionalSkewPercent,
            ComparableDistrictsUsed: forecast.Metadata.ComparableDistrictsUsed,
            ModelVersion: forecast.Metadata.ModelVersion,
            Draws: forecast.Metadata.Draws);
    }
}

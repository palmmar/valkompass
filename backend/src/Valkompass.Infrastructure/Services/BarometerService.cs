using Microsoft.EntityFrameworkCore;
using Valkompass.Application.Contracts;
using Valkompass.Application.Dtos;
using Valkompass.Domain.Entities;
using Valkompass.Infrastructure.Persistence;

namespace Valkompass.Infrastructure.Services;

/// <summary>
/// Läser valbarometerns opinionsdata och härleder tidsserier, snitt och senaste läget.
/// Rör aldrig quiz-/resultatdata. Faktiska valresultat (institutkod "val-…") hålls åtskilda
/// från opinionsmätningar i alla aggregat.
/// </summary>
public class BarometerService(AppDbContext db) : IBarometerService
{
    private const int RollingWindowDays = 30;

    private const string Disclaimer =
        "Detta är opinionsmätningar – inte en prognos och inte ett valresultat. Varje prick är en " +
        "enskild mätning med statistisk osäkerhet (felmarginal). Källa är respektive institut " +
        "(primärkälla); data levereras via SwedishPolls och SCB:s öppna data. Partier under ett " +
        "instituts redovisningsgräns visas som \"redovisas ej\", aldrig som 0 %.";

    private static bool IsElection(Poll p) => p.Pollster!.Code.StartsWith("val-");

    public async Task<IReadOnlyList<BarometerPollDto>> GetPollsAsync(
        string? pollsterCode, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var order = await PartyOrderAsync(ct);

        var query = db.Polls.Include(p => p.Pollster).Include(p => p.Results).AsNoTracking()
            .Where(p => (from == null || p.PublishedAt >= from) && (to == null || p.PublishedAt <= to));

        // Utan institutfilter listas bara opinionsmätningar (faktiska valresultat utelämnas).
        query = pollsterCode is { Length: > 0 }
            ? query.Where(p => p.Pollster!.Code == pollsterCode)
            : query.Where(p => !p.Pollster!.Code.StartsWith("val-"));

        var polls = await query.OrderByDescending(p => p.PublishedAt).ToListAsync(ct);
        return polls.Select(p => MapPoll(p, order)).ToList();
    }

    public async Task<BarometerLatestDto?> GetLatestAsync(CancellationToken ct = default)
    {
        var order = await PartyOrderAsync(ct);
        var parties = await PartiesAsync(ct);

        var latest = await db.Polls.Include(p => p.Pollster).Include(p => p.Results).AsNoTracking()
            .Where(p => !p.Pollster!.Code.StartsWith("val-"))
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (latest is null) return null;

        // Senaste riksdagsval före (eller på) mätningens datum – för förändring i procentenheter.
        var election = await db.Polls.Include(p => p.Pollster).Include(p => p.Results).AsNoTracking()
            .Where(p => p.Pollster!.Code.StartsWith("val-") && p.PublishedAt <= latest.PublishedAt)
            .OrderByDescending(p => p.PublishedAt)
            .FirstOrDefaultAsync(ct);

        var electionByParty = election?.Results.ToDictionary(r => r.PartyId, r => r.Value) ?? [];
        var latestByParty = latest.Results.ToDictionary(r => r.PartyId);

        var results = order
            .Select(kv =>
            {
                latestByParty.TryGetValue(kv.Key, out var r);
                var value = r?.Value;
                var moe = r is null ? null : r.MarginOfError ?? ComputeMoe(r.Value, latest.SampleSize);
                double? delta = null;
                if (value is not null && electionByParty.TryGetValue(kv.Key, out var ev) && ev is not null)
                    delta = Math.Round(value.Value - ev.Value, 1);
                return new { kv.Value.Code, kv.Value.Order, Dto = new BarometerLatestResultDto(kv.Value.Code, value, moe, delta) };
            })
            .OrderBy(x => x.Order)
            .Select(x => x.Dto)
            .ToList();

        int? electionYear = election is null ? null : YearFromCode(election.Pollster!.Code);
        return new BarometerLatestDto(parties, MapPoll(latest, order), electionYear, results, Disclaimer);
    }

    public async Task<BarometerTimeseriesDto> GetTimeseriesAsync(
        DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var parties = await PartiesAsync(ct);
        var order = await PartyOrderAsync(ct);
        var codeById = order.ToDictionary(kv => kv.Key, kv => kv.Value.Code);

        var polls = await db.Polls.Include(p => p.Pollster).Include(p => p.Results).AsNoTracking()
            .Where(p => (from == null || p.PublishedAt >= from) && (to == null || p.PublishedAt <= to))
            .OrderBy(p => p.PublishedAt).ThenBy(p => p.Id)
            .ToListAsync(ct);

        var opinion = polls.Where(p => !IsElection(p)).ToList();
        var elections = polls.Where(IsElection).ToList();

        // Enskilda mätpunkter per parti (bara redovisade värden – null kan inte plottas).
        var pointsByParty = parties.ToDictionary(p => p.Code, _ => new List<BarometerPointDto>());
        foreach (var poll in opinion)
        {
            foreach (var r in poll.Results)
            {
                if (r.Value is null || !codeById.TryGetValue(r.PartyId, out var code)) continue;
                var moe = r.MarginOfError ?? ComputeMoe(r.Value, poll.SampleSize);
                pointsByParty[code].Add(new BarometerPointDto(
                    poll.PublishedAt, r.Value.Value, moe, poll.Pollster!.Code, poll.SampleSize, poll.SourceUrl));
            }
        }

        var series = parties.Select(p => new BarometerSeriesDto(p.Code, pointsByParty[p.Code])).ToList();

        // Månadssnitt: enkelt medelvärde per kalendermånad (mittmånad), för läsbarhet.
        var monthly = parties.Select(p =>
        {
            var pts = pointsByParty[p.Code]
                .GroupBy(x => (x.Date.Year, x.Date.Month))
                .Select(g => new BarometerAvgPointDto(
                    new DateOnly(g.Key.Year, g.Key.Month, 15), Math.Round(g.Average(x => x.Value), 2), MeanMoe(g)))
                .OrderBy(x => x.Date).ToList();
            return new BarometerAvgSeriesDto(p.Code, pts);
        }).ToList();

        // Glidande snitt (poll-of-polls): medelvärde av alla mätningar inom de senaste 30 dagarna.
        var rolling = parties.Select(p =>
        {
            var pts = pointsByParty[p.Code]; // redan sorterade på datum
            var line = new List<BarometerAvgPointDto>();
            foreach (var pt in pts)
            {
                var lo = pt.Date.AddDays(-RollingWindowDays);
                var window = pts.Where(x => x.Date > lo && x.Date <= pt.Date).ToList();
                line.Add(new BarometerAvgPointDto(pt.Date, Math.Round(window.Average(x => x.Value), 2), MeanMoe(window)));
            }
            var dedup = line.GroupBy(x => x.Date).Select(g => g.Last()).OrderBy(x => x.Date).ToList();
            return new BarometerAvgSeriesDto(p.Code, dedup);
        }).ToList();

        var electionDtos = elections.Select(e => new BarometerElectionDto(
            e.PublishedAt,
            e.Pollster!.DisplayName,
            order.Where(kv => e.Results.Any(r => r.PartyId == kv.Key))
                 .OrderBy(kv => kv.Value.Order)
                 .Select(kv => new BarometerPartyValueDto(
                     kv.Value.Code, e.Results.First(r => r.PartyId == kv.Key).Value))
                 .ToList()))
            .OrderBy(e => e.Date).ToList();

        return new BarometerTimeseriesDto(
            parties, series, monthly, rolling, RollingWindowDays, electionDtos, from, to, Disclaimer);
    }

    // --- Hjälpare ---

    /// <summary>95-procentig schablon-felmarginal i procentenheter ur n och värdet, eller null.</summary>
    private static double? ComputeMoe(double? valuePct, int? n)
    {
        if (valuePct is null || n is null or <= 0) return null;
        var p = valuePct.Value / 100.0;
        if (p is <= 0 or >= 1) return null;
        return Math.Round(1.96 * Math.Sqrt(p * (1 - p) / n.Value) * 100.0, 1);
    }

    /// <summary>Medelfelmarginal för en grupp mätpunkter (ignorerar punkter utan felmarginal).</summary>
    private static double? MeanMoe(IEnumerable<BarometerPointDto> points)
    {
        var moes = points.Where(x => x.MarginOfError is not null).Select(x => x.MarginOfError!.Value).ToList();
        return moes.Count == 0 ? null : Math.Round(moes.Average(), 2);
    }

    private static int? YearFromCode(string code) =>
        int.TryParse(code.AsSpan(code.LastIndexOf('-') + 1), out var y) ? y : null;

    private BarometerPollDto MapPoll(Poll p, IReadOnlyDictionary<int, (string Code, int Order)> order)
    {
        var results = p.Results
            .Where(r => order.ContainsKey(r.PartyId))
            .OrderBy(r => order[r.PartyId].Order)
            .Select(r => new BarometerPollResultDto(order[r.PartyId].Code, r.Value, r.MarginOfError ?? ComputeMoe(r.Value, p.SampleSize)))
            .ToList();

        return new BarometerPollDto(
            p.ExternalKey, p.Pollster!.Code, p.Pollster.DisplayName, p.Pollster.Method,
            p.FieldStart, p.FieldEnd, p.PublishedAt, p.SampleSize, p.SourceUrl, p.SourceCitation, results);
    }

    private async Task<IReadOnlyList<BarometerPartyDto>> PartiesAsync(CancellationToken ct) =>
        await db.Parties.AsNoTracking().OrderBy(p => p.DisplayOrder)
            .Select(p => new BarometerPartyDto(p.Code, p.Name, p.Color, p.DisplayOrder))
            .ToListAsync(ct);

    private async Task<IReadOnlyDictionary<int, (string Code, int Order)>> PartyOrderAsync(CancellationToken ct) =>
        await db.Parties.AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => (p.Code, p.DisplayOrder), ct);
}

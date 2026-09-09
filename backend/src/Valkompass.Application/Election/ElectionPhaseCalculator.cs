using Valkompass.Domain.Enums;

namespace Valkompass.Application.Election;

/// <summary>Tidsgränser för valdagen. Sätts i konfiguration så att genrep kan testas mot andra datum.</summary>
public sealed class ElectionTimeline
{
    public const string SectionName = "ElectionTimeline";

    /// <summary>När vallokalerna stänger. Valdagen 2026 är söndag 13 september kl. 20:00.</summary>
    public DateTimeOffset PollsCloseAt { get; set; } =
        new(2026, 9, 13, 20, 0, 0, TimeSpan.FromHours(2));

    /// <summary>
    /// Hur länge en snapshot får vara utan uppdatering innan källan räknas som fördröjd.
    /// </summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Avgör vilken fas valvakan är i. Rent och tidsstyrt via parametrar, så att hela valkvällen
/// går att testa utan att vänta på den.
/// </summary>
public static class ElectionPhaseCalculator
{
    /// <summary>Tidsstämplarna i Valmyndighetens filer avser svensk tid.</summary>
    public static readonly TimeZoneInfo SwedishTime = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "W. Europe Standard Time" : "Europe/Stockholm");

    public static ElectionPhase Resolve(
        ElectionTimeline timeline,
        ElectionSnapshot? snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        // Före stängning avgör bara klockan: det finns inga resultat att ta hänsyn till.
        if (now < timeline.PollsCloseAt)
        {
            return IsSameSwedishDate(now, timeline.PollsCloseAt)
                ? ElectionPhase.ElectionDay
                : ElectionPhase.PreElection;
        }

        // Efter stängning styr datan. Vallokalerna kan ha stängt utan att något rapporterats.
        if (snapshot is null || snapshot.Reporting.DistrictsReported == 0)
        {
            return ElectionPhase.WaitingForResults;
        }

        var complete = snapshot.Reporting.DistrictsReported >= snapshot.Reporting.DistrictsTotal;

        return snapshot.Source.Stage switch
        {
            CountingStage.Final => complete ? ElectionPhase.Finished : ElectionPhase.FinalCounting,
            // Preliminärt färdigräknat är inte samma sak som klart: onsdagens uppsamlings-
            // räkning och den slutliga räkningen återstår.
            _ => complete ? ElectionPhase.PreliminaryPaused : ElectionPhase.Live,
        };
    }

    /// <summary>
    /// Sant när källan inte uppdaterats på ett tag. Fristående från fasen: en fördröjd källa
    /// ändrar inte var i rösträkningen vi är, den säger bara att siffrorna är de senaste vi
    /// har. Frontend visar då senaste giltiga snapshot märkt som fördröjd.
    /// </summary>
    public static bool IsStale(ElectionTimeline timeline, ElectionSnapshot? snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        return snapshot is not null && now - snapshot.Source.UpdatedAt > timeline.StaleAfter;
    }

    private static bool IsSameSwedishDate(DateTimeOffset a, DateTimeOffset b) =>
        TimeZoneInfo.ConvertTime(a, SwedishTime).Date == TimeZoneInfo.ConvertTime(b, SwedishTime).Date;
}

namespace Valkompass.Application.Election;

/// <summary>Vad importen ska göra just nu.</summary>
public enum ImportAction
{
    /// <summary>Hämta från Valmyndigheten.</summary>
    Import,

    /// <summary>Rösträkningen har inte börjat än – vila och kolla igen.</summary>
    Wait,

    /// <summary>Manuellt avstängd via nödutgången.</summary>
    Paused,

    /// <summary>Importfönstret är passerat. Det finns inget mer att hämta.</summary>
    Finished,
}

/// <param name="Action">Vad som ska hända.</param>
/// <param name="Delay">Hur länge tjänsten ska vila innan den frågar igen.</param>
public readonly record struct ImportDecision(ImportAction Action, TimeSpan Delay);

/// <summary>
/// Bestämmer när importen ska vara igång. Ren funktion av tid och konfiguration, så att hela
/// valveckan går att testa utan att vänta på den.
/// </summary>
/// <remarks>
/// Importen startar av sig själv strax före vallokalerna stänger och håller på genom onsdagens
/// uppsamlingsräkning och den slutliga räkningen. Ingen behöver komma ihåg att slå på den, och
/// framför allt behöver ingen starta om servern mitt under rösträkningen för att göra det.
///
/// Att polla innan det finns resultat kostar ingenting: ett tomt <c>index.md5</c> ger
/// <c>NoResultsPublished</c>. Vinsten är att vägen är genomgången och bevisat fungerande innan
/// den behövs på riktigt.
/// </remarks>
public static class ElectionImportSchedule
{
    /// <summary>Längsta vila när vi bara väntar på att fönstret ska öppna.</summary>
    public static readonly TimeSpan MaxIdleDelay = TimeSpan.FromMinutes(15);

    public static ImportDecision Decide(ElectionTimeline timeline, ElectionImport options, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(options);

        if (now > WindowEnd(timeline))
        {
            return new ImportDecision(ImportAction.Finished, TimeSpan.Zero);
        }

        if (!options.Enabled)
        {
            // Nödutgången. Vi fortsätter kolla, så att en påslagning hittas inom ett
            // pollintervall i stället för att kräva omstart.
            return new ImportDecision(ImportAction.Paused, options.PollInterval);
        }

        var start = WindowStart(timeline);
        if (now < start)
        {
            var untilStart = start - now;
            return new ImportDecision(
                ImportAction.Wait, untilStart < MaxIdleDelay ? untilStart : MaxIdleDelay);
        }

        return new ImportDecision(ImportAction.Import, options.PollInterval);
    }

    public static DateTimeOffset WindowStart(ElectionTimeline timeline) =>
        timeline.PollsCloseAt - timeline.ImportStartsBefore;

    public static DateTimeOffset WindowEnd(ElectionTimeline timeline) =>
        timeline.PollsCloseAt + timeline.ImportStopsAfter;
}

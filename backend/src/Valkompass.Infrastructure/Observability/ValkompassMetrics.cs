using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics;
using Valkompass.Domain.Enums;

namespace Valkompass.Infrastructure.Observability;

/// <summary>
/// Appens egna mätvärden: trafiken genom kompassen och valvakans import.
/// </summary>
/// <remarks>
/// Instrumenten är vanliga <see cref="System.Diagnostics.Metrics"/>-instrument, alltså samma
/// API som ASP.NET Core själv mäter med. Exportören (OpenTelemetry → Prometheus) sitter i
/// API-projektet och kan bytas ut utan att en enda rad här ändras.
///
/// Räknarna nollställs när podden startar om – det är normalt för Prometheus, som hanterar
/// omstarter i <c>rate()</c> och <c>increase()</c>. Värden som måste överleva en omstart
/// (totalsummor, tidsstämplar) läses i stället ur databasen av
/// <see cref="MetricsRefreshBackgroundService"/> och rapporteras som observerbara mätare.
/// </remarks>
public sealed class ValkompassMetrics : IDisposable
{
    /// <summary>Meter-namnet som exportören måste lyssna på.</summary>
    public const string MeterName = "Valkompass";

    private readonly Meter _meter;
    private readonly Counter<long> _quizStarted;
    private readonly Counter<long> _quizCompleted;
    private readonly Counter<long> _importRuns;
    private readonly Histogram<double> _importDuration;

    // Skrivs av bakgrundstjänsten, läses av mätarnas callbacks. Referensbytet är atomärt och
    // snapshoten är oföränderlig, så ingen låsning behövs.
    private volatile MetricsSnapshot _database = MetricsSnapshot.Empty;

    public ValkompassMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);

        _quizStarted = _meter.CreateCounter<long>(
            "valkompass.quiz.started",
            unit: "{kompass}",
            description: "Påbörjade kompasser (anonym signal från klienten).");

        _quizCompleted = _meter.CreateCounter<long>(
            "valkompass.quiz.completed",
            unit: "{kompass}",
            description: "Slutförda kompasser med sparat resultat.");

        _importRuns = _meter.CreateCounter<long>(
            "valkompass.election.import.runs",
            unit: "{försök}",
            description: "Importvarv mot Valmyndigheten, per utfall.");

        _importDuration = _meter.CreateHistogram(
            "valkompass.election.import.duration",
            unit: "s",
            description: "Tid för att hämta, verifiera och spara en ny resultatfil.",
            tags: null,
            advice: ImportDurationBuckets);

        // Totalsumman som överlever omstart. Auktoritativ: en rad per faktiskt sparat resultat.
        _meter.CreateObservableGauge(
            "valkompass.quiz.sessions_stored",
            () => Observe(_database.StoredQuizSessions),
            unit: "{kompass}",
            description: "Antal sparade kompassresultat i databasen (sedan start).");

        // Valvakans färskhet. Skillnaden mot Valmyndighetens egen tidsstämpel är poängen:
        // source_updated_at säger hur färska siffrorna är, ingested_at när vi senast lyckades
        // hämta dem. Larma på time() - ingested_at när importfönstret är öppet.
        _meter.CreateObservableGauge(
            "valkompass.election.snapshot.ingested_at",
            () => ObserveElection(e => e.IngestedAt.ToUnixTimeMilliseconds() / 1000d),
            unit: "s",
            description: "Unixtid då valvakan senast fick ny data från Valmyndigheten.");

        _meter.CreateObservableGauge(
            "valkompass.election.snapshot.source_updated_at",
            () => ObserveElection(e => e.SourceUpdatedAt.ToUnixTimeMilliseconds() / 1000d),
            unit: "s",
            description: "Unixtid som Valmyndigheten själv stämplat siffrorna med.");

        _meter.CreateObservableGauge(
            "valkompass.election.districts.reported",
            () => ObserveElection(e => (double)e.DistrictsReported),
            unit: "{valdistrikt}",
            description: "Antal färdigräknade valdistrikt i senaste snapshoten.");

        _meter.CreateObservableGauge(
            "valkompass.election.districts.expected",
            () => ObserveElection(e => (double)e.DistrictsTotal),
            unit: "{valdistrikt}",
            description: "Totalt antal valdistrikt i senaste snapshoten.");
    }

    // Standardhinkarna i OpenTelemetry (0, 5, 10, 25 … 10000) är gjorda för
    // millisekunder; i sekunder hade varje importvarv hamnat i samma hink. Gränserna
    // här spänner över det ett varv faktiskt tar: en ZIP på några tiotal megabyte som
    // ska laddas ned, signaturkontrolleras, tolkas och sparas.
    private static readonly InstrumentAdvice<double> ImportDurationBuckets = new()
    {
        HistogramBucketBoundaries = [0.25, 0.5, 1, 2, 5, 10, 20, 30, 60],
    };

    /// <summary>En kompass har påbörjats. <paramref name="mode"/> 0 = okänt läge.</summary>
    public void QuizStarted(int mode, QuizVariant variant) =>
        _quizStarted.Add(1, ModeTag(mode), VariantTag(variant));

    /// <summary>En kompass har slutförts och resultatet sparats.</summary>
    public void QuizCompleted(int mode, QuizVariant variant) =>
        _quizCompleted.Add(1, ModeTag(mode), VariantTag(variant));

    /// <summary>Ett avslutat importvarv. <paramref name="outcome"/> blir en label i Prometheus.</summary>
    public void ElectionImportRun(string outcome) =>
        _importRuns.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>Tiden ett lyckat importvarv tog, från nedladdning till sparad snapshot.</summary>
    public void ElectionImportDuration(TimeSpan elapsed) =>
        _importDuration.Record(elapsed.TotalSeconds);

    /// <summary>Byter ut databasens ögonblicksbild. Anropas av bakgrundstjänsten.</summary>
    public void UpdateDatabaseSnapshot(MetricsSnapshot snapshot) =>
        _database = snapshot ?? MetricsSnapshot.Empty;

    public void Dispose() => _meter.Dispose();

    // Okända värden rapporteras som ingen mätpunkt alls i stället för en nolla: en saknad
    // tidsstämpel är inte "1 januari 1970", och en tom graf är ärligare än en påhittad nolla.
    private static IEnumerable<Measurement<double>> Observe(double? value) =>
        value is { } v ? [new Measurement<double>(v)] : [];

    private IEnumerable<Measurement<double>> ObserveElection(Func<ElectionSnapshotMetrics, double> select) =>
        _database.Election is { } election ? [new Measurement<double>(select(election))] : [];

    private static KeyValuePair<string, object?> ModeTag(int mode) =>
        new("mode", mode > 0 ? mode.ToString() : "unknown");

    private static KeyValuePair<string, object?> VariantTag(QuizVariant variant) =>
        new("variant", variant == QuizVariant.Swipe ? "swipe" : "standard");
}

/// <summary>
/// Det som måste läsas ur databasen för att överleva en omstart. Oföränderlig: bakgrundstjänsten
/// byter ut hela instansen, mätarnas callbacks läser den som den är.
/// </summary>
/// <param name="StoredQuizSessions">Antal sparade kompassresultat, eller null innan första läsningen.</param>
/// <param name="Election">Senaste snapshot som faktiskt serveras, eller null om ingen finns.</param>
public sealed record MetricsSnapshot(long? StoredQuizSessions, ElectionSnapshotMetrics? Election)
{
    public static readonly MetricsSnapshot Empty = new(null, null);
}

/// <summary>Valvakans läge, hämtat ur senaste sparade snapshot.</summary>
public sealed record ElectionSnapshotMetrics(
    DateTimeOffset IngestedAt,
    DateTimeOffset SourceUpdatedAt,
    int DistrictsReported,
    int DistrictsTotal);

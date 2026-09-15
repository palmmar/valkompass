namespace Valkompass.Infrastructure.Observability;

/// <summary>
/// Konfiguration för mätvärdena. Sätts via <c>Metrics__*</c> som miljövariabler.
/// </summary>
public sealed class MetricsOptions
{
    public const string SectionName = "Metrics";

    /// <summary>
    /// Hur ofta de databasgrundade mätarna läses om (totalt antal kompasser, valvakans läge).
    /// Standardvärdet är kortare än ett normalt skrapintervall, så siffrorna hinner aldrig bli
    /// gamla i grafen. Korta ned det under en valnatt om rapporteringsgraden ska följas tätare;
    /// varje varv är två små frågor mot databasen.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Skyddar mot att ett nollställt eller negativt intervall snurrar fritt.</summary>
    public TimeSpan EffectiveRefreshInterval =>
        RefreshInterval < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : RefreshInterval;
}

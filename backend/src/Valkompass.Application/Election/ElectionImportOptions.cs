namespace Valkompass.Application.Election;

/// <summary>
/// Konfiguration för importen av Valmyndighetens resultat. Genrep och produktion skiljs åt
/// med <see cref="BaseUrl"/> och <see cref="AllowTestData"/>.
/// </summary>
public sealed class ElectionImport
{
    public const string SectionName = "ElectionImport";

    /// <summary>
    /// Slår av importen helt. Ska gå att stänga av utan att sidan går sönder – den senast
    /// sparade snapshoten fortsätter serveras (#87).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Katalogen som innehåller <c>index.md5</c>. Produktion:
    /// <c>https://resultat.val.se/resultatfiler/val2026/</c>. Genrep:
    /// <c>https://resultat.val.se/resultatfiler/genrep2026/</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "https://resultat.val.se/resultatfiler/val2026/";

    /// <summary>
    /// Tillåter att data märkt <c>test: true</c> sparas. Måste vara false i produktion –
    /// annars kan genrepssiffror visas som verkligt valresultat.
    /// </summary>
    public bool AllowTestData { get; set; }

    /// <summary>Hur ofta indexet kontrolleras. Valnatten uppdateras filerna ofta.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Väntetid efter ett misslyckat försök. Dubblas upp till <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Timeout per HTTP-anrop. ZIP-filen är några megabyte.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Hur gammal senaste lyckade import får vara innan källan räknas som fördröjd. Frontend
    /// visar då senaste giltiga snapshot märkt som fördröjd i stället för ingenting alls.
    /// </summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(10);
}

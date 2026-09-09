namespace Valkompass.Application.Election;

/// <summary>
/// Konfiguration för importen av Valmyndighetens resultat. Genrep och produktion skiljs åt
/// med <see cref="BaseUrl"/> och <see cref="AllowTestData"/>.
/// </summary>
public sealed class ElectionImport
{
    public const string SectionName = "ElectionImport";

    /// <summary>
    /// Nödutgång, inte startknapp. Importen styrs av tiden (se
    /// <see cref="ElectionImportSchedule"/>) och behöver inte slås på manuellt. Sätt false för
    /// att stoppa den om något går fel – senast sparade snapshot fortsätter serveras (#87).
    /// </summary>
    public bool Enabled { get; set; } = true;

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

    /// <summary>
    /// Verifierar Valmyndighetens signaturfiler mot deras certifikat. Går att stänga av som
    /// nödutgång om certifikatet skulle bytas oväntat mitt under rösträkningen – då faller vi
    /// tillbaka på checksummekontrollen i stället för att sluta visa resultat.
    /// </summary>
    public bool VerifySignatures { get; set; } = true;

    /// <summary>
    /// Beräknar och sparar prognosen (#84). Av som standard: det räknade resultatet är en
    /// fullgod valvaka i sig, och prognosen ska slås på medvetet först när dess kalibrering
    /// setts mot skarpa siffror.
    /// </summary>
    public bool Forecast { get; set; }

    /// <summary>Valmyndighetens publika signeringscertifikat (PEM).</summary>
    public string CertificateUrl { get; set; } = "https://resultat.val.se/keys/val-sign-crt.pem";

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

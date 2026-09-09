using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Valkompass.Application.Contracts;
using Valkompass.Application.Election;
using Valkompass.Application.Election.Nowcast;
using Valkompass.Domain.Enums;

namespace Valkompass.Infrastructure.Services;

/// <summary>Vad ett importförsök ledde till.</summary>
public enum ImportOutcome
{
    /// <summary>Indexet är tomt – rösträkningen har inte börjat publiceras än.</summary>
    NoResultsPublished,

    /// <summary>Checksumman var oförändrad, ingen ZIP laddades ned.</summary>
    Unchanged,

    /// <summary>En ny snapshot importerades och sparades.</summary>
    Imported,

    /// <summary>Data märkt som test avvisades eftersom testdata inte är tillåten.</summary>
    RejectedTestData,
}

/// <summary>
/// Ett importvarv mot Valmyndigheten: läs <c>index.md5</c>, jämför checksumman mot senast
/// sparade, ladda ned och tolka ZIP-filen om den ändrats, och spara resultatet.
/// </summary>
/// <remarks>
/// Kastar vid nätverks- och formatfel. Anroparen (<see cref="ElectionImportBackgroundService"/>)
/// äger backoff och loggning, och en misslyckad import lämnar alltid föregående snapshot orörd.
/// </remarks>
public class ElectionResultImporter(
    HttpClient http,
    IElectionSnapshotStore store,
    ElectionSignatureVerifier signatures,
    IOptions<ElectionImport> options,
    TimeProvider time,
    ILogger<ElectionResultImporter> logger)
{
    private readonly ElectionImport _options = options.Value;

    public async Task<ImportOutcome> ImportAsync(
        CountingStage stage = CountingStage.Preliminary,
        CancellationToken ct = default)
    {
        var index = ElectionIndex.Parse(await http.GetStringAsync(IndexUrl(), ct));
        var entry = ElectionIndex.FindParliamentaryResult(index, stage);
        if (entry is null)
        {
            logger.LogDebug("Inget riksdagsresultat publicerat än ({Count} filer i indexet).", index.Count);
            return ImportOutcome.NoResultsPublished;
        }

        var lastChecksum = await store.GetLatestChecksumAsync(stage, ct);
        if (string.Equals(lastChecksum, entry.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            return ImportOutcome.Unchanged;
        }

        logger.LogInformation(
            "Ny checksumma för {File}: {Checksum} (föregående {Previous}). Hämtar.",
            entry.FileName, entry.Checksum, lastChecksum ?? "ingen");

        var started = time.GetTimestamp();
        var files = await DownloadAsync(entry, ct);

        var snapshot = ElectionResultParser.ParseMandateFile(
            new MemoryStream(files.Mandates, writable: false), entry.Checksum, time.GetUtcNow());

        if (snapshot.Source.IsTest && !_options.AllowTestData)
        {
            // Genrepsdata i en produktionskonfiguration. Att låta den passera vore att
            // publicera påhittade siffror som valresultat.
            logger.LogWarning(
                "Avvisade {File}: datan är märkt test:true och AllowTestData är av.",
                entry.FileName);
            return ImportOutcome.RejectedTestData;
        }

        var forecast = BuildForecast(files, snapshot);

        var saved = await store.SaveAsync(snapshot, forecast, ct);
        logger.LogInformation(
            "Importerade {File} på {Elapsed}: {Reported}/{Total} valdistrikt, uppdaterad {UpdatedAt}. Ny rad: {Saved}.",
            entry.FileName,
            time.GetElapsedTime(started),
            snapshot.Reporting.DistrictsReported,
            snapshot.Reporting.DistrictsTotal,
            snapshot.Source.UpdatedAt,
            saved);

        return ImportOutcome.Imported;
    }

    /// <summary>
    /// Beräknar prognosen, när den är påslagen.
    /// </summary>
    /// <remarks>
    /// Fel fångas medvetet här. Prognosen är ett tillägg till det räknade resultatet, aldrig
    /// en förutsättning för det – går modellen fel ska sidan visa Valmyndighetens siffror som
    /// vanligt, utan prognos, i stället för ingenting alls.
    /// </remarks>
    private NowcastResult? BuildForecast(ArchiveFiles files, ElectionSnapshot snapshot)
    {
        if (!_options.Forecast)
        {
            return null;
        }

        try
        {
            var input = new NowcastInput(
                NowcastInputParser.ParseDistricts(files.Districts),
                NowcastInputParser.ParseMunicipalities(files.Summary),
                snapshot.Reporting.TotalVotesPrevious ?? 0);

            var result = NowcastEngine.Run(input);

            if (result.Available)
            {
                logger.LogInformation(
                    "Prognos beräknad: {Districts} jämförbara distrikt, {Coverage} % täckning, "
                    + "typisk osäkerhet ±{Uncertainty} pp, geografisk skevhet {Skew} %.",
                    result.Metadata!.ComparableDistrictsUsed,
                    result.Metadata.CoveragePercent,
                    result.Metadata.TypicalUncertaintyPoints,
                    result.Metadata.RegionalSkewPercent);
            }
            else
            {
                logger.LogInformation("Ingen prognos: {Reason}", result.UnavailableReason);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Prognosen kunde inte beräknas. Det räknade resultatet sparas ändå.");
            return null;
        }
    }

    /// <summary>De tre JSON-filerna i ett resultatarkiv.</summary>
    /// <param name="Mandates">Rikets röstfördelning och mandat, ca 300 kB.</param>
    /// <param name="Summary">Kommunnivån, ca 2 MB.</param>
    /// <param name="Districts">Valdistrikten, ca 40 MB. Behövs bara för prognosen.</param>
    private sealed record ArchiveFiles(byte[] Mandates, byte[] Summary, byte[] Districts);

    /// <summary>
    /// Hämtar ZIP-filen, verifierar den mot checksumman i indexet och packar upp de tre
    /// JSON-filerna, var och en kontrollerad mot sin signatur.
    /// </summary>
    private async Task<ArchiveFiles> DownloadAsync(ElectionIndexEntry entry, CancellationToken ct)
    {
        using var response = await http.GetAsync(
            ResultUrl(entry), HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var zip = new MemoryStream();
        await using (var body = await response.Content.ReadAsStreamAsync(ct))
        {
            await body.CopyToAsync(zip, ct);
        }

        zip.Position = 0;
        VerifyChecksum(zip, entry);

        zip.Position = 0;
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);

        return new ArchiveFiles(
            await ReadVerifiedAsync(archive, entry, "mandatfordelning", ct),
            await ReadVerifiedAsync(archive, entry, "summering", ct),
            await ReadVerifiedAsync(archive, entry, "rostfordelning", ct));
    }

    /// <summary>Packar upp en namngiven JSON-fil och kontrollerar dess signatur.</summary>
    private async Task<byte[]> ReadVerifiedAsync(
        ZipArchive archive,
        ElectionIndexEntry entry,
        string name,
        CancellationToken ct)
    {
        var file = archive.Entries.FirstOrDefault(e =>
            e.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
            && e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            ?? throw new ElectionResultFormatException(
                $"{entry.FileName} innehåller ingen {name}-fil.");

        var json = await ReadEntryAsync(file, ct);

        // Signaturen ligger bredvid JSON-filen: Namn.json -> Namn_sign.sha256.
        var signatureName = $"{file.Name[..^".json".Length]}_sign.sha256";
        var signatureEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, signatureName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ElectionResultFormatException(
                $"{entry.FileName} saknar signaturfilen {signatureName}.");

        await signatures.VerifyAsync(json, await ReadEntryAsync(signatureEntry, ct), file.Name, ct);

        return json;
    }

    /// <summary>Kopierar ut en post så att ZIP-strömmen kan stängas direkt.</summary>
    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await using (var stream = entry.Open())
        {
            await stream.CopyToAsync(buffer, ct);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Valmyndigheten publicerar även signaturfiler, men verifiering av dem kräver deras
    /// publika certifikat. Checksumman ur <c>index.md5</c> går att kontrollera direkt och
    /// fångar avbrutna och trasiga nedladdningar.
    /// </summary>
    private static void VerifyChecksum(Stream zip, ElectionIndexEntry entry)
    {
        var actual = Convert.ToHexString(MD5.HashData(zip)).ToLowerInvariant();
        if (!string.Equals(actual, entry.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            throw new ElectionResultFormatException(
                $"Checksumman för {entry.FileName} stämmer inte: väntade {entry.Checksum}, fick {actual}.");
        }
    }

    private Uri IndexUrl() => new(BaseUri(), "index.md5");

    /// <summary>Sökvägarna i indexet är relativa till samma katalog som indexfilen.</summary>
    private Uri ResultUrl(ElectionIndexEntry entry) =>
        new(BaseUri(), entry.RelativePath.TrimStart('.', '/'));

    private Uri BaseUri()
    {
        var url = _options.BaseUrl;
        return new Uri(url.EndsWith('/') ? url : url + "/", UriKind.Absolute);
    }
}

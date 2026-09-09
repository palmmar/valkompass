using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Valkompass.Application.Contracts;
using Valkompass.Application.Election;
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
        var archive = await DownloadAsync(entry, ct);

        var snapshot = ElectionResultParser.ParseMandateFile(
            archive, entry.Checksum, time.GetUtcNow());

        if (snapshot.Source.IsTest && !_options.AllowTestData)
        {
            // Genrepsdata i en produktionskonfiguration. Att låta den passera vore att
            // publicera påhittade siffror som valresultat.
            logger.LogWarning(
                "Avvisade {File}: datan är märkt test:true och AllowTestData är av.",
                entry.FileName);
            return ImportOutcome.RejectedTestData;
        }

        var saved = await store.SaveAsync(snapshot, ct);
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
    /// Hämtar ZIP-filen, verifierar den mot checksumman i indexet och packar upp
    /// mandatfördelningsfilen.
    /// </summary>
    private async Task<Stream> DownloadAsync(ElectionIndexEntry entry, CancellationToken ct)
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
        var file = archive.Entries.FirstOrDefault(e =>
            e.Name.Contains("mandatfordelning", StringComparison.OrdinalIgnoreCase)
            && e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            ?? throw new ElectionResultFormatException(
                $"{entry.FileName} innehåller ingen mandatfordelning-fil.");

        var json = await ReadEntryAsync(file, ct);

        // Signaturen ligger bredvid JSON-filen: Namn.json -> Namn_sign.sha256.
        var signatureName = $"{file.Name[..^".json".Length]}_sign.sha256";
        var signatureEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, signatureName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ElectionResultFormatException(
                $"{entry.FileName} saknar signaturfilen {signatureName}.");

        await signatures.VerifyAsync(json, await ReadEntryAsync(signatureEntry, ct), file.Name, ct);

        return new MemoryStream(json, writable: false);
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

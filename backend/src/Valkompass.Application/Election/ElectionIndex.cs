using Valkompass.Domain.Enums;

namespace Valkompass.Application.Election;

/// <summary>En rad i Valmyndighetens <c>index.md5</c>.</summary>
public sealed record ElectionIndexEntry(string Checksum, string RelativePath)
{
    /// <summary>Filnamnet utan katalogdel, t.ex. <c>Val_2026_preliminar_00_RD.zip</c>.</summary>
    public string FileName => RelativePath[(RelativePath.LastIndexOf('/') + 1)..];
}

/// <summary>
/// Tolkar <c>index.md5</c>, som listar checksumman för varje publicerad resultatfil.
/// Valmyndigheten rekommenderar att den används för att upptäcka vilka ZIP-filer som ändrats,
/// så att vi slipper ladda ned oförändrade filer.
/// </summary>
/// <remarks>
/// Formatet är md5sum-radder: <c>&lt;checksumma&gt;&lt;två blanksteg&gt;./p/rd/Fil.zip</c>.
/// Före valnatten svarar produktionsindexet med md5-summan för tom indata och sökvägen
/// <c>-</c>; det betyder "inga resultat ännu" och är inte ett fel.
/// </remarks>
public static class ElectionIndex
{
    /// <summary>Riksdagsvalet publiceras som valområde 00, valtyp RD.</summary>
    private const string ParliamentarySuffix = "_00_RD.zip";

    public static IReadOnlyList<ElectionIndexEntry> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var entries = new List<ElectionIndexEntry>();
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var separator = trimmed.IndexOf(' ');
            if (separator <= 0)
            {
                continue;
            }

            var checksum = trimmed[..separator];
            var path = trimmed[separator..].TrimStart();

            // "-" är md5sums beteckning för stdin och används när indexet är tomt.
            if (path.Length == 0 || path == "-")
            {
                continue;
            }

            entries.Add(new ElectionIndexEntry(checksum, path));
        }

        return entries;
    }

    /// <summary>
    /// Hittar riksdagsvalets resultatfil för ett räkningstillfälle, eller null om den ännu
    /// inte publicerats. Matchar på filnamn i stället för sökväg, så att samma kod fungerar
    /// för både genrep (<c>Genrep_2026_…</c>) och skarpt val (<c>Val_2026_…</c>).
    /// </summary>
    public static ElectionIndexEntry? FindParliamentaryResult(
        IReadOnlyList<ElectionIndexEntry> entries,
        CountingStage stage)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var stageToken = stage switch
        {
            CountingStage.Preliminary => "_preliminar_",
            CountingStage.Final => "_slutlig_",
            _ => throw new ArgumentOutOfRangeException(nameof(stage)),
        };

        return entries.FirstOrDefault(e =>
            e.FileName.EndsWith(ParliamentarySuffix, StringComparison.OrdinalIgnoreCase)
            && e.FileName.Contains(stageToken, StringComparison.OrdinalIgnoreCase));
    }
}

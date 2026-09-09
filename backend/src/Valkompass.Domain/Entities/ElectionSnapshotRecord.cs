using Valkompass.Domain.Enums;

namespace Valkompass.Domain.Entities;

/// <summary>
/// En importerad rösträkningssnapshot från Valmyndigheten, sparad så att den överlever en
/// omstart. Coolify deployar om vid varje push till main, och backend kan startas om mitt
/// under rösträkningen – då ska senaste giltiga läge finnas kvar (#87).
/// </summary>
/// <remarks>
/// Tabellen är append-only: varje ny checksumma blir en ny rad. Under en valnatt handlar det
/// om några hundra rader à några kilobyte, och historiken gör det möjligt att i efterhand se
/// exakt vad sidan visade när. <see cref="Payload"/> är den normaliserade snapshoten som JSON,
/// inte Valmyndighetens råa filinnehåll.
/// </remarks>
public class ElectionSnapshotRecord
{
    public long Id { get; set; }

    public CountingStage Stage { get; set; }

    /// <summary>Filens checksumma ur <c>index.md5</c>. Unik per räkningstillfälle.</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Sant för genrepsdata (<c>test</c> i källfilen). Sådana rader får aldrig serveras som
    /// verkligt valresultat.
    /// </summary>
    public bool IsTest { get; set; }

    /// <summary>Valmyndighetens egen tidsstämpel för när siffrorna senast uppdaterades.</summary>
    public DateTimeOffset SourceUpdatedAt { get; set; }

    /// <summary>När vi hämtade och accepterade snapshoten.</summary>
    public DateTimeOffset IngestedAt { get; set; }

    public int DistrictsReported { get; set; }

    public int DistrictsTotal { get; set; }

    /// <summary>Den normaliserade snapshoten serialiserad som JSON (jsonb).</summary>
    public string Payload { get; set; } = string.Empty;
}

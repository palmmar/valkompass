namespace Valkompass.Domain.Entities;

/// <summary>
/// En enskild mätning från ett institut. Naturlig nyckel: <see cref="ExternalKey"/>
/// (t.ex. "novus-2026-04"), så att ingest seedas idempotent. All proveniens lagras per
/// mätning: institut, fältperiod, antal svar (n) och käll-URL.
/// </summary>
public class Poll
{
    public int Id { get; set; }

    /// <summary>Stabil naturlig nyckel, t.ex. "novus-2026-04". Unik.</summary>
    public required string ExternalKey { get; set; }

    public int PollsterId { get; set; }
    public Pollster? Pollster { get; set; }

    /// <summary>Fältperiodens start (insamling). Null om okänd.</summary>
    public DateOnly? FieldStart { get; set; }

    /// <summary>Fältperiodens slut (insamling). Null om okänd.</summary>
    public DateOnly? FieldEnd { get; set; }

    /// <summary>Publiceringsdatum. Detta är tidsaxeln i diagrammen.</summary>
    public DateOnly PublishedAt { get; set; }

    /// <summary>Antal svar (n). Null om ej redovisat.</summary>
    public int? SampleSize { get; set; }

    public string? SourceUrl { get; set; }

    /// <summary>Källhänvisning, t.ex. "Novus/SVT, fältperiod 2026-04-01–2026-04-14".</summary>
    public string? SourceCitation { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<PollResult> Results { get; set; } = new List<PollResult>();
}

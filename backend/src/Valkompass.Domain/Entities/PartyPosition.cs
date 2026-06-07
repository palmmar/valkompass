using Valkompass.Domain.Enums;

namespace Valkompass.Domain.Entities;

/// <summary>Ett partis ställningstagande på en specifik fråga.</summary>
public class PartyPosition
{
    public int Id { get; set; }

    public int PartyId { get; set; }
    public Party? Party { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }

    /// <summary>
    /// Partiets position 1–4. Null = oklar/ingen position → frågan exkluderas för detta
    /// parti i matchningen (men inte för övriga partier).
    /// </summary>
    public ScaleValue? Value { get; set; }

    /// <summary>Partiets motivering (visas på resultatsidan).</summary>
    public string? Motivation { get; set; }

    /// <summary>Källhänvisning, t.ex. "Valmanifest 2026, s. 12".</summary>
    public string? SourceCitation { get; set; }

    public string? SourceUrl { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

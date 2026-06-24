using Valkompass.Domain.Enums;

namespace Valkompass.Domain.Entities;

/// <summary>
/// Anonym telemetrihändelse för att mäta trafik genom kompassen (påbörjade/slutförda per läge).
/// Medvetet helt frikopplad från <c>QuizSession</c>: ingen token, inga svar, ingen IP – bara
/// typ, läge, variant och tidsstämpel. Möjliggör aggregat som funnel och lägespopularitet.
/// </summary>
public class QuizEvent
{
    public long Id { get; set; }

    public QuizEventType Type { get; set; }

    /// <summary>Valt läge: antal frågor (25/50/75). 0 = okänt.</summary>
    public int Mode { get; set; }

    public QuizVariant Variant { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

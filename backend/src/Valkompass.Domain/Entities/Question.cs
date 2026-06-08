namespace Valkompass.Domain.Entities;

/// <summary>Ett påstående som användaren tar ställning till.</summary>
public class Question
{
    public int Id { get; set; }

    /// <summary>Stabil naturlig nyckel som används vid idempotent seedning, t.ex. "sjukvard-vinster".</summary>
    public required string ExternalKey { get; set; }

    /// <summary>Själva påståendet, t.ex. "Vinster i välfärden ska förbjudas".</summary>
    public required string Text { get; set; }

    /// <summary>Förklarande text som visas på resultatsidan (vad frågan egentligen handlar om).</summary>
    public string? Explanation { get; set; }

    /// <summary>Valfri källänk ("Läs mer") till information om nuläget bakom påståendet.</summary>
    public string? ExplanationSourceUrl { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Soft-enable. Inaktiva frågor visas inte i quizet men finns kvar för historiska resultat.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<PartyPosition> Positions { get; set; } = new List<PartyPosition>();
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}

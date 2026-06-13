namespace Valkompass.Domain.Entities;

/// <summary>
/// Ett opinionsinstitut (Novus, Demoskop, SCB …). Behandlas som <em>primärkälla</em> för
/// en mätning; SwedishPolls/SCB-API:t är bara leveranskanal. Naturlig nyckel: <see cref="Code"/>.
/// </summary>
public class Pollster
{
    public int Id { get; set; }

    /// <summary>Kort institutkod, t.ex. "novus", "demoskop", "scb". Unik.</summary>
    public required string Code { get; set; }

    /// <summary>Visningsnamn, t.ex. "Novus".</summary>
    public required string DisplayName { get; set; }

    /// <summary>Insamlingsmetod, t.ex. "Webbpanel", "Telefon", "Post". Null = okänd.</summary>
    public string? Method { get; set; }

    /// <summary>Ev. uppdragsgivare (medium/organisation som beställt mätningen).</summary>
    public string? Commissioner { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Poll> Polls { get; set; } = new List<Poll>();
}

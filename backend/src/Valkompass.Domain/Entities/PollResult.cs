namespace Valkompass.Domain.Entities;

/// <summary>
/// Ett partis resultat i en specifik mätning.
/// </summary>
public class PollResult
{
    public int Id { get; set; }

    public int PollId { get; set; }
    public Poll? Poll { get; set; }

    public int PartyId { get; set; }
    public Party? Party { get; set; }

    /// <summary>
    /// Stöd i procent, eller <c>null</c> när partiet ligger under institutets
    /// redovisningsgräns ("redovisas ej"). Lagras och visas <strong>aldrig som 0</strong> —
    /// samma null-invariant som <see cref="PartyPosition.Value"/>.
    /// </summary>
    public double? Value { get; set; }

    /// <summary>
    /// Ev. felmarginal i procentenheter. Null = ej angiven av institutet; API:t beräknar
    /// då en 95-procentig schablon från n och värdet.
    /// </summary>
    public double? MarginOfError { get; set; }
}

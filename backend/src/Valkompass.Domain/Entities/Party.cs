namespace Valkompass.Domain.Entities;

/// <summary>Ett parti i kompassen (de åtta riksdagspartierna).</summary>
public class Party
{
    public int Id { get; set; }

    /// <summary>Kort partikod, t.ex. "S", "M", "SD". Unik.</summary>
    public required string Code { get; set; }

    /// <summary>Vanligt namn, t.ex. "Moderaterna".</summary>
    public required string Name { get; set; }

    /// <summary>Officiellt fullständigt namn, t.ex. "Moderata samlingspartiet".</summary>
    public required string FullName { get; set; }

    /// <summary>Kort beskrivning av partiet (visas på resultatsidan).</summary>
    public string? ShortDescription { get; set; }

    /// <summary>Hex-färg för UI, t.ex. "#1B4F9C".</summary>
    public string? Color { get; set; }

    public string? LogoUrl { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PartyPosition> Positions { get; set; } = new List<PartyPosition>();
}

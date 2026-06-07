namespace Valkompass.Domain.Entities;

/// <summary>Politikområde som frågor grupperas under, t.ex. "Sjukvård".</summary>
public class Category
{
    public int Id { get; set; }

    /// <summary>URL-vänlig nyckel, t.ex. "sjukvard". Unik och stabil (används vid seedning).</summary>
    public required string Slug { get; set; }

    /// <summary>Visningsnamn, t.ex. "Sjukvård".</summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>Valfri ikon-identifierare för UI.</summary>
    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}

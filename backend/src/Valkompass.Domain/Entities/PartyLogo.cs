namespace Valkompass.Domain.Entities;

/// <summary>
/// Ett partis logotyp lagrad som binärdata i en egen tabell, så att den (potentiellt stora)
/// blobben inte dras med i vanliga partifrågor. En logotyp per parti.
/// </summary>
public class PartyLogo
{
    /// <summary>Primärnyckel och främmande nyckel till <see cref="Party"/>.</summary>
    public int PartyId { get; set; }

    public Party? Party { get; set; }

    /// <summary>Bildens råa byte (PNG eller WebP).</summary>
    public required byte[] Data { get; set; }

    /// <summary>MIME-typ, t.ex. "image/png" eller "image/webp".</summary>
    public required string ContentType { get; set; }

    /// <summary>När logotypen senast sattes. Används för ETag/cache.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

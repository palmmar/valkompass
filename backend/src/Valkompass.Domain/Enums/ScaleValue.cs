namespace Valkompass.Domain.Enums;

/// <summary>
/// Fyrgradig skala utan neutralt mittenalternativ. Lagras rått som 1–4.
/// Matchningslogiken mappar dessa till centrerade värden (-1.5, -0.5, +0.5, +1.5).
/// "Hoppa över" (användare) och "oklar position" (parti) representeras INTE här —
/// de uttrycks som null/flagga och betyder "ingen signal".
/// </summary>
public enum ScaleValue
{
    /// <summary>Håller inte med.</summary>
    StronglyDisagree = 1,

    /// <summary>Håller delvis inte med.</summary>
    PartlyDisagree = 2,

    /// <summary>Håller delvis med.</summary>
    PartlyAgree = 3,

    /// <summary>Håller helt med.</summary>
    StronglyAgree = 4,
}

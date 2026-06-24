namespace Valkompass.Domain.Enums;

/// <summary>Vilken variant av kompassen som användes.</summary>
public enum QuizVariant
{
    /// <summary>Vanligt test med fyrgradig knappskala.</summary>
    Standard = 0,

    /// <summary>Förenklat binärt swajp-läge (experimentellt).</summary>
    Swipe = 1,
}

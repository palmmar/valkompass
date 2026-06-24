namespace Valkompass.Domain.Enums;

/// <summary>Typ av anonymt quiz-telemetrihändelse (se <c>QuizEvent</c>).</summary>
public enum QuizEventType
{
    /// <summary>Användaren började svara på kompassen (första svaret).</summary>
    Started = 0,

    /// <summary>Kompassen skickades in och ett resultat sparades.</summary>
    Completed = 1,
}

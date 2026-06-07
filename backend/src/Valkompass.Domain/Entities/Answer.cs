using Valkompass.Domain.Enums;

namespace Valkompass.Domain.Entities;

/// <summary>Ett enskilt svar i ett quiz-tillfälle.</summary>
public class Answer
{
    public int Id { get; set; }

    public Guid QuizSessionId { get; set; }
    public QuizSession? QuizSession { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }

    /// <summary>Användarens svar 1–4, eller null om frågan hoppades över.</summary>
    public ScaleValue? Value { get; set; }

    /// <summary>True om frågan hoppades över (då är Value null och frågan exkluderas i matchningen).</summary>
    public bool IsSkipped { get; set; }

    /// <summary>"Extra viktig"-markering som höjer frågans vikt i matchningen.</summary>
    public bool IsImportant { get; set; }
}

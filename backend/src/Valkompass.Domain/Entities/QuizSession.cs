namespace Valkompass.Domain.Entities;

/// <summary>Ett anonymt quiz-tillfälle med beräknat resultat, nåbart via delningslänk.</summary>
public class QuizSession
{
    public Guid Id { get; set; }

    /// <summary>Oguessbar, URL-säker token som utgör delningslänken. Unik.</summary>
    public required string ShareToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Fryst ögonblicksbild (jsonb) av det beräknade resultatet vid inlämning, så att
    /// delade länkar förblir stabila även om frågor/positioner redigeras senare.
    /// </summary>
    public required string ResultJson { get; set; }

    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}

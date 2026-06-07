namespace Valkompass.Application.Dtos;

/// <summary>Frågeformuläret som visas i quizet – innehåller medvetet INGA partipositioner.</summary>
public sealed record QuestionnaireDto(
    IReadOnlyList<QuestionnaireCategoryDto> Categories,
    IReadOnlyList<QuestionnaireQuestionDto> Questions);

public sealed record QuestionnaireCategoryDto(string Slug, string Name, string? Description, int DisplayOrder);

public sealed record QuestionnaireQuestionDto(int Id, string Text, string? Explanation, string CategorySlug);

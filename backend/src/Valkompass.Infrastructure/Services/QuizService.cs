using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Valkompass.Application.Content;
using Valkompass.Application.Contracts;
using Valkompass.Application.Dtos;
using Valkompass.Application.Matching;
using Valkompass.Domain.Entities;
using Valkompass.Domain.Enums;
using Valkompass.Infrastructure.Persistence;

namespace Valkompass.Infrastructure.Services;

public class QuizService(AppDbContext db) : IQuizService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<QuestionnaireDto> GetQuestionnaireAsync(CancellationToken ct = default)
    {
        var questions = await db.Questions
            .Where(q => q.IsActive)
            .Include(q => q.Category)
            .OrderBy(q => q.Category!.DisplayOrder).ThenBy(q => q.DisplayOrder)
            .ToListAsync(ct);

        // Bara kategorier som faktiskt har aktiva frågor.
        var categories = questions
            .Select(q => q.Category!)
            .DistinctBy(c => c.Id)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new QuestionnaireCategoryDto(c.Slug, c.Name, c.Description, c.DisplayOrder))
            .ToList();

        var questionDtos = questions
            .Select(q => new QuestionnaireQuestionDto(q.Id, q.Text, q.Explanation, q.Category!.Slug))
            .ToList();

        return new QuestionnaireDto(categories, questionDtos);
    }

    public async Task<SubmitOutcome> SubmitAsync(SubmitQuizRequest request, CancellationToken ct = default)
    {
        if (request.Answers is null || request.Answers.Count == 0)
            return SubmitOutcome.Invalid("Inga svar skickades in.");

        var questions = await db.Questions
            .Where(q => q.IsActive)
            .Include(q => q.Category)
            .ToListAsync(ct);
        var questionById = questions.ToDictionary(q => q.Id);

        // --- Validering ---
        var errors = new List<string>();
        var seen = new HashSet<int>();
        foreach (var a in request.Answers)
        {
            if (!questionById.ContainsKey(a.QuestionId))
                errors.Add($"Okänd eller inaktiv fråga: {a.QuestionId}.");
            else if (!seen.Add(a.QuestionId))
                errors.Add($"Dubblettsvar för fråga {a.QuestionId}.");

            if (!a.IsSkipped && a.Value is not (>= 1 and <= 4))
                errors.Add($"Ogiltigt värde för fråga {a.QuestionId} (måste vara 1–4 eller hoppas över).");
        }
        if (errors.Count > 0)
            return SubmitOutcome.Invalid([.. errors]);

        var parties = await db.Parties.Where(p => p.IsActive).OrderBy(p => p.DisplayOrder).ToListAsync(ct);
        var activeQuestionIds = questionById.Keys.ToHashSet();
        var activePartyIds = parties.Select(p => p.Id).ToHashSet();

        var positions = await db.PartyPositions
            .Where(p => activeQuestionIds.Contains(p.QuestionId) && activePartyIds.Contains(p.PartyId))
            .ToListAsync(ct);
        var positionByKey = positions.ToDictionary(p => (p.PartyId, p.QuestionId));

        // --- Bygg indata till matchningen ---
        var matchAnswers = request.Answers
            .Select(a => new MatchAnswer(
                a.QuestionId,
                a.IsSkipped ? null : (ScaleValue?)a.Value,
                a.IsSkipped,
                a.IsImportant))
            .ToList();

        var matchQuestions = questions.Select(q => new MatchQuestion(q.Id, q.CategoryId)).ToList();
        var matchParties = parties.Select(p => new MatchParty(p.Id, p.Code, p.DisplayOrder)).ToList();
        var matchPositions = positions.Select(p => new MatchPartyPosition(p.PartyId, p.QuestionId, p.Value)).ToList();

        var match = MatchCalculator.Calculate(matchAnswers, matchQuestions, matchParties, matchPositions);

        // --- Bygg det frysta resultatdokumentet ---
        var document = BuildDocument(match, parties, questionById, positionByKey);

        // --- Spara session + svar ---
        var session = new QuizSession
        {
            Id = Guid.CreateVersion7(),
            ShareToken = await GenerateUniqueTokenAsync(ct),
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            ResultJson = JsonSerializer.Serialize(document, JsonOptions),
        };

        foreach (var a in request.Answers)
        {
            session.Answers.Add(new Answer
            {
                QuestionId = a.QuestionId,
                Value = a.IsSkipped ? null : (ScaleValue?)a.Value,
                IsSkipped = a.IsSkipped,
                IsImportant = a.IsImportant,
            });
        }

        db.QuizSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return SubmitOutcome.Success(session.ShareToken);
    }

    public async Task<ResultDocument?> GetResultAsync(string token, CancellationToken ct = default)
    {
        var json = await db.QuizSessions
            .Where(s => s.ShareToken == token)
            .Select(s => s.ResultJson)
            .FirstOrDefaultAsync(ct);

        return json is null ? null : JsonSerializer.Deserialize<ResultDocument>(json, JsonOptions);
    }

    private static ResultDocument BuildDocument(
        MatchResult match,
        IReadOnlyList<Party> parties,
        IReadOnlyDictionary<int, Question> questionById,
        IReadOnlyDictionary<(int PartyId, int QuestionId), PartyPosition> positionByKey)
    {
        var partyById = parties.ToDictionary(p => p.Id);

        var partyRefs = parties
            .Select(p => new ResultPartyRefDto(p.Code, p.Name, p.FullName, p.Color, p.ShortDescription))
            .ToList();

        var overall = match.Overall
            .Select(s => new ResultPartyScoreDto(s.PartyCode, s.AgreementPct, s.ComparedQuestionCount))
            .ToList();

        // Kategori-id → (slug, namn, ordning) via frågornas kategorier.
        var categoryById = questionById.Values
            .Select(q => q.Category!)
            .DistinctBy(c => c.Id)
            .ToDictionary(c => c.Id);

        var categories = match.ByCategory
            .Where(c => categoryById.ContainsKey(c.CategoryId))
            .Select(c =>
            {
                var cat = categoryById[c.CategoryId];
                var scores = c.Parties
                    .Select(s => new ResultPartyScoreDto(s.PartyCode, s.AgreementPct, s.ComparedQuestionCount))
                    .ToList();
                return (cat.DisplayOrder, Dto: new ResultCategoryDto(cat.Slug, cat.Name, scores));
            })
            .OrderBy(x => x.DisplayOrder)
            .Select(x => x.Dto)
            .ToList();

        var partyIdByCode = parties.ToDictionary(p => p.Code, p => p.Id);

        var questions = match.ByQuestion
            .Where(q => questionById.ContainsKey(q.QuestionId))
            .Select(q =>
            {
                var entity = questionById[q.QuestionId];
                var partyDtos = q.Parties.Select(pc =>
                {
                    positionByKey.TryGetValue((partyIdByCode[pc.PartyCode], q.QuestionId), out var pos);
                    return new ResultQuestionPartyDto(
                        pc.PartyCode,
                        (int?)pc.PartyValue,
                        pc.AgreementPct,
                        pos?.Motivation,
                        pos?.SourceCitation,
                        pos?.SourceUrl);
                }).ToList();

                return (CatOrder: entity.Category!.DisplayOrder, QOrder: entity.DisplayOrder, Dto: new ResultQuestionDto(
                    q.QuestionId,
                    entity.ExternalKey,
                    entity.Text,
                    entity.Explanation,
                    entity.Category!.Slug,
                    entity.Category!.Name,
                    (int?)q.UserValue,
                    q.Skipped,
                    q.IsImportant,
                    partyDtos));
            })
            .OrderBy(x => x.CatOrder).ThenBy(x => x.QOrder)
            .Select(x => x.Dto)
            .ToList();

        return new ResultDocument(
            DateTimeOffset.UtcNow, partyRefs, overall, categories, questions, Disclaimers.Result);
    }

    private async Task<string> GenerateUniqueTokenAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var token = GenerateToken();
            if (!await db.QuizSessions.AnyAsync(s => s.ShareToken == token, ct))
                return token;
        }
        throw new InvalidOperationException("Kunde inte generera en unik delningstoken.");
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[16]; // 128 bitar
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }
}

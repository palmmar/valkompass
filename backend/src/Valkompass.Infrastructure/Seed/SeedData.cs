using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Valkompass.Domain.Entities;
using Valkompass.Domain.Enums;
using Valkompass.Infrastructure.Persistence;

namespace Valkompass.Infrastructure.Seed;

/// <summary>
/// Idempotent seedning av innehåll från inbäddade JSON-filer. Upsertar på naturliga nycklar
/// (kategori-slug, partikod, frågans externalKey) så att omkörning är säker och
/// innehållsändringar flödar in vid uppstart.
/// </summary>
public static class SeedData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task SeedContentAsync(AppDbContext db, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        // --- Kategorier (upsert på slug) ---
        var categorySeeds = Load<CategorySeed>("categories.json");
        var categories = await db.Categories.ToDictionaryAsync(c => c.Slug, ct);
        foreach (var c in categorySeeds)
        {
            if (categories.TryGetValue(c.Slug, out var entity))
            {
                entity.Name = c.Name;
                entity.Description = c.Description;
                entity.Icon = c.Icon;
                entity.DisplayOrder = c.DisplayOrder;
            }
            else
            {
                db.Categories.Add(new Category
                {
                    Slug = c.Slug, Name = c.Name, Description = c.Description,
                    Icon = c.Icon, DisplayOrder = c.DisplayOrder,
                });
            }
        }
        await db.SaveChangesAsync(ct);

        // --- Partier (upsert på code) ---
        var partySeeds = Load<PartySeed>("parties.json");
        var parties = await db.Parties.ToDictionaryAsync(p => p.Code, ct);
        foreach (var p in partySeeds)
        {
            if (parties.TryGetValue(p.Code, out var entity))
            {
                entity.Name = p.Name;
                entity.FullName = p.FullName;
                entity.ShortDescription = p.ShortDescription;
                entity.Color = p.Color;
                entity.DisplayOrder = p.DisplayOrder;
                entity.IsActive = true;
            }
            else
            {
                db.Parties.Add(new Party
                {
                    Code = p.Code, Name = p.Name, FullName = p.FullName,
                    ShortDescription = p.ShortDescription, Color = p.Color,
                    DisplayOrder = p.DisplayOrder, IsActive = true,
                });
            }
        }
        await db.SaveChangesAsync(ct);

        // --- Frågor (upsert på externalKey) ---
        var categoryIdBySlug = await db.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id, ct);
        var questionSeeds = Load<QuestionSeed>("questions.json");
        var questions = await db.Questions.ToDictionaryAsync(q => q.ExternalKey, ct);
        foreach (var q in questionSeeds)
        {
            if (!categoryIdBySlug.TryGetValue(q.CategorySlug, out var categoryId))
                continue; // okänd kategori – hoppa över

            if (questions.TryGetValue(q.ExternalKey, out var entity))
            {
                entity.Text = q.Text;
                entity.Explanation = q.Explanation;
                entity.ExplanationSourceUrl = q.ExplanationSourceUrl;
                entity.CategoryId = categoryId;
                entity.DisplayOrder = q.DisplayOrder;
                entity.Tier = q.Tier ?? 3;
                entity.IsActive = true;
                entity.UpdatedAt = now;
            }
            else
            {
                db.Questions.Add(new Question
                {
                    ExternalKey = q.ExternalKey, Text = q.Text, Explanation = q.Explanation,
                    ExplanationSourceUrl = q.ExplanationSourceUrl,
                    CategoryId = categoryId, DisplayOrder = q.DisplayOrder, Tier = q.Tier ?? 3,
                    IsActive = true, CreatedAt = now, UpdatedAt = now,
                });
            }
        }
        await db.SaveChangesAsync(ct);

        // --- Positioner (upsert på (partikod, frågenyckel)) ---
        var partyIdByCode = await db.Parties.ToDictionaryAsync(p => p.Code, p => p.Id, ct);
        var questionIdByKey = await db.Questions.ToDictionaryAsync(q => q.ExternalKey, q => q.Id, ct);
        var positionByKey = (await db.PartyPositions.ToListAsync(ct))
            .ToDictionary(p => (p.PartyId, p.QuestionId));
        var positionSeeds = Load<PositionSeed>("positions.json");
        foreach (var pos in positionSeeds)
        {
            if (!partyIdByCode.TryGetValue(pos.PartyCode, out var partyId)) continue;
            if (!questionIdByKey.TryGetValue(pos.QuestionKey, out var questionId)) continue;

            var value = pos.Value is null ? (ScaleValue?)null : (ScaleValue)pos.Value.Value;
            if (positionByKey.TryGetValue((partyId, questionId), out var entity))
            {
                entity.Value = value;
                entity.Motivation = pos.Motivation;
                entity.SourceCitation = pos.SourceCitation;
                entity.SourceUrl = pos.SourceUrl;
                entity.UpdatedAt = now;
            }
            else
            {
                db.PartyPositions.Add(new PartyPosition
                {
                    PartyId = partyId, QuestionId = questionId, Value = value,
                    Motivation = pos.Motivation, SourceCitation = pos.SourceCitation,
                    SourceUrl = pos.SourceUrl, UpdatedAt = now,
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static T[] Load<T>(string fileName)
    {
        var assembly = typeof(SeedData).Assembly;
        var resourceName = $"Valkompass.Infrastructure.Seed.Content.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Inbäddad resurs saknas: {resourceName}");
        return JsonSerializer.Deserialize<T[]>(stream, JsonOptions) ?? [];
    }

    private sealed record CategorySeed(string Slug, string Name, string? Description, string? Icon, int DisplayOrder);
    private sealed record PartySeed(string Code, string Name, string FullName, string? ShortDescription, string? Color, int DisplayOrder);
    private sealed record QuestionSeed(string ExternalKey, string CategorySlug, string Text, string? Explanation, string? ExplanationSourceUrl, int DisplayOrder, int? Tier);
    private sealed record PositionSeed(string PartyCode, string QuestionKey, int? Value, string? Motivation, string? SourceCitation, string? SourceUrl);
}

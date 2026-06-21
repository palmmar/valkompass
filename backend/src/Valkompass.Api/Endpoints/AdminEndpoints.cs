using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Valkompass.Application.Dtos;
using Valkompass.Domain.Entities;
using Valkompass.Domain.Enums;
using Valkompass.Domain.Identity;
using Valkompass.Infrastructure.Persistence;

namespace Valkompass.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization("EditorOrAdmin");

        MapCategories(admin);
        MapParties(admin);
        MapQuestions(admin);
        MapPositions(admin);
        MapStats(admin);

        return app;
    }

    private static void MapCategories(RouteGroupBuilder admin)
    {
        admin.MapGet("/categories", async (AppDbContext db) =>
            Results.Ok(await db.Categories.OrderBy(c => c.DisplayOrder)
                .Select(c => new AdminCategoryDto(c.Id, c.Slug, c.Name, c.Description, c.Icon, c.DisplayOrder))
                .ToListAsync()));

        admin.MapPost("/categories", async (CategoryInput input, AppDbContext db) =>
        {
            var entity = new Category
            {
                Slug = input.Slug, Name = input.Name, Description = input.Description,
                Icon = input.Icon, DisplayOrder = input.DisplayOrder,
            };
            db.Categories.Add(entity);
            var conflict = await SaveOrConflict(db, "En kategori med samma slug finns redan.");
            return conflict ?? Results.Created($"/api/admin/categories/{entity.Id}", Map(entity));
        });

        admin.MapPut("/categories/{id:int}", async (int id, CategoryInput input, AppDbContext db) =>
        {
            var entity = await db.Categories.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Slug = input.Slug; entity.Name = input.Name; entity.Description = input.Description;
            entity.Icon = input.Icon; entity.DisplayOrder = input.DisplayOrder;
            var conflict = await SaveOrConflict(db, "En kategori med samma slug finns redan.");
            return conflict ?? Results.Ok(Map(entity));
        });

        admin.MapDelete("/categories/{id:int}", async (int id, AppDbContext db) =>
        {
            var entity = await db.Categories.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Categories.Remove(entity);
            return await SaveOrConflict(db, "Kategorin har frågor och kan inte tas bort.") ?? Results.NoContent();
        }).RequireAuthorization("AdminOnly");
    }

    private static void MapParties(RouteGroupBuilder admin)
    {
        admin.MapGet("/parties", async (AppDbContext db) =>
            Results.Ok(await db.Parties.OrderBy(p => p.DisplayOrder)
                .Select(p => new AdminPartyDto(
                    p.Id, p.Code, p.Name, p.FullName, p.ShortDescription, p.Color, p.DisplayOrder, p.IsActive,
                    db.PartyLogos.Any(l => l.PartyId == p.Id)))
                .ToListAsync()));

        admin.MapPost("/parties", async (PartyInput input, AppDbContext db) =>
        {
            var entity = new Party
            {
                Code = input.Code, Name = input.Name, FullName = input.FullName,
                ShortDescription = input.ShortDescription, Color = input.Color,
                DisplayOrder = input.DisplayOrder, IsActive = input.IsActive,
            };
            db.Parties.Add(entity);
            var conflict = await SaveOrConflict(db, "Ett parti med samma kod finns redan.");
            return conflict ?? Results.Created($"/api/admin/parties/{entity.Id}", Map(entity, hasLogo: false));
        });

        admin.MapPut("/parties/{id:int}", async (int id, PartyInput input, AppDbContext db) =>
        {
            var entity = await db.Parties.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Code = input.Code; entity.Name = input.Name; entity.FullName = input.FullName;
            entity.ShortDescription = input.ShortDescription; entity.Color = input.Color;
            entity.DisplayOrder = input.DisplayOrder; entity.IsActive = input.IsActive;
            var conflict = await SaveOrConflict(db, "Ett parti med samma kod finns redan.");
            if (conflict is not null) return conflict;
            var hasLogo = await db.PartyLogos.AnyAsync(l => l.PartyId == entity.Id);
            return Results.Ok(Map(entity, hasLogo));
        });

        admin.MapDelete("/parties/{id:int}", async (int id, AppDbContext db) =>
        {
            var entity = await db.Parties.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Parties.Remove(entity);
            return await SaveOrConflict(db, "Partiet kan inte tas bort.") ?? Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        // Ladda upp/ersätt ett partis logotyp (PNG eller WebP). Lagras som binärdata i DB.
        admin.MapPost("/parties/{id:int}/logo", async (int id, IFormFile file, AppDbContext db) =>
        {
            var party = await db.Parties.FindAsync(id);
            if (party is null) return Results.NotFound();
            if (file.Length == 0) return Results.ValidationProblem(Field("file", "Filen är tom."));
            if (file.Length > MaxLogoBytes)
                return Results.ValidationProblem(Field("file", "Filen är för stor (max 512 kB)."));

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var data = ms.ToArray();
            if (DetectImageType(data) is null)
                return Results.ValidationProblem(Field("file", "Endast PNG och WebP stöds."));

            // Skala ner till en rimlig maxstorlek och spara som PNG, så att sidan inte tyngs av
            // logotyper i full upplösning (visas i 20–56 px). Lagrar de omskalade byten.
            byte[] png;
            try
            {
                png = await ResizeToPngAsync(data);
            }
            catch (ImageFormatException)
            {
                return Results.ValidationProblem(Field("file", "Kunde inte läsa bilden."));
            }

            var logo = await db.PartyLogos.FindAsync(id);
            if (logo is null)
            {
                db.PartyLogos.Add(new PartyLogo
                {
                    PartyId = id, Data = png, ContentType = "image/png", UpdatedAt = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                logo.Data = png; logo.ContentType = "image/png"; logo.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        }).DisableAntiforgery();

        admin.MapDelete("/parties/{id:int}/logo", async (int id, AppDbContext db) =>
        {
            var logo = await db.PartyLogos.FindAsync(id);
            if (logo is null) return Results.NotFound();
            db.PartyLogos.Remove(logo);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private const long MaxLogoBytes = 512 * 1024;
    private const int MaxLogoDimension = 256; // logotyper visas i 20–56 px; 256 räcker även för retina

    /// <summary>Skalar ner bilden så längsta sidan är högst 256 px (skalar aldrig upp) och kodar om till PNG.</summary>
    private static async Task<byte[]> ResizeToPngAsync(byte[] data)
    {
        using var image = Image.Load(data);
        if (image.Width > MaxLogoDimension || image.Height > MaxLogoDimension)
        {
            image.Mutate(c => c.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxLogoDimension, MaxLogoDimension),
            }));
        }

        using var output = new MemoryStream();
        await image.SaveAsPngAsync(output);
        return output.ToArray();
    }

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Returnerar MIME-typen om byten är en PNG eller WebP, annars null (validering via magic bytes).</summary>
    private static string? DetectImageType(byte[] b)
    {
        if (b.Length >= 8 && b.AsSpan(0, 8).SequenceEqual(PngMagic)) return "image/png";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
            && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        return null;
    }

    private static void MapQuestions(RouteGroupBuilder admin)
    {
        admin.MapGet("/questions", async (AppDbContext db) =>
            Results.Ok(await db.Questions.Include(q => q.Category)
                .OrderBy(q => q.Category!.DisplayOrder).ThenBy(q => q.DisplayOrder)
                .Select(q => Map(q)).ToListAsync()));

        admin.MapGet("/questions/{id:int}", async (int id, AppDbContext db) =>
        {
            var q = await db.Questions.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
            return q is null ? Results.NotFound() : Results.Ok(Map(q));
        });

        admin.MapPost("/questions", async (QuestionInput input, AppDbContext db) =>
        {
            if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId))
                return Results.ValidationProblem(Field("categoryId", "Okänd kategori."));
            if (input.Tier is < 1 or > 3)
                return Results.ValidationProblem(Field("tier", "Nivån måste vara 1, 2 eller 3."));

            var now = DateTimeOffset.UtcNow;
            var entity = new Question
            {
                ExternalKey = input.ExternalKey, Text = input.Text, Explanation = input.Explanation,
                ExplanationSourceUrl = input.ExplanationSourceUrl,
                CategoryId = input.CategoryId, DisplayOrder = input.DisplayOrder, Tier = input.Tier,
                IsActive = input.IsActive,
                CreatedAt = now, UpdatedAt = now,
            };
            db.Questions.Add(entity);
            var conflict = await SaveOrConflict(db, "En fråga med samma externalKey finns redan.");
            if (conflict is not null) return conflict;
            await db.Entry(entity).Reference(e => e.Category).LoadAsync();
            return Results.Created($"/api/admin/questions/{entity.Id}", Map(entity));
        });

        admin.MapPut("/questions/{id:int}", async (int id, QuestionInput input, AppDbContext db) =>
        {
            var entity = await db.Questions.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (!await db.Categories.AnyAsync(c => c.Id == input.CategoryId))
                return Results.ValidationProblem(Field("categoryId", "Okänd kategori."));
            if (input.Tier is < 1 or > 3)
                return Results.ValidationProblem(Field("tier", "Nivån måste vara 1, 2 eller 3."));

            entity.ExternalKey = input.ExternalKey; entity.Text = input.Text; entity.Explanation = input.Explanation;
            entity.ExplanationSourceUrl = input.ExplanationSourceUrl;
            entity.CategoryId = input.CategoryId; entity.DisplayOrder = input.DisplayOrder; entity.Tier = input.Tier;
            entity.IsActive = input.IsActive;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            var conflict = await SaveOrConflict(db, "En fråga med samma externalKey finns redan.");
            if (conflict is not null) return conflict;
            await db.Entry(entity).Reference(e => e.Category).LoadAsync();
            return Results.Ok(Map(entity));
        });

        admin.MapDelete("/questions/{id:int}", async (int id, AppDbContext db) =>
        {
            var entity = await db.Questions.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Questions.Remove(entity);
            return await SaveOrConflict(db, "Frågan ingår i sparade resultat och kan inte tas bort. Inaktivera den i stället.")
                ?? Results.NoContent();
        }).RequireAuthorization("AdminOnly");
    }

    private static void MapPositions(RouteGroupBuilder admin)
    {
        admin.MapGet("/questions/{id:int}/positions", async (int id, AppDbContext db) =>
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null) return Results.NotFound();

            var parties = await db.Parties.OrderBy(p => p.DisplayOrder).ToListAsync();
            var byParty = (await db.PartyPositions.Where(p => p.QuestionId == id).ToListAsync())
                .ToDictionary(p => p.PartyId);

            var dtos = parties.Select(p =>
            {
                byParty.TryGetValue(p.Id, out var pos);
                return new AdminPositionDto(
                    p.Id, p.Code, p.Name, (int?)pos?.Value, pos?.Motivation, pos?.SourceCitation, pos?.SourceUrl);
            }).ToList();

            return Results.Ok(new PositionsMatrixDto(question.Id, question.Text, dtos));
        });

        admin.MapPut("/questions/{id:int}/positions", async (int id, PositionsBulkInput input, AppDbContext db) =>
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null) return Results.NotFound();

            foreach (var p in input.Positions)
                if (p.Value is not (null or (>= 1 and <= 4)))
                    return Results.ValidationProblem(Field("value", "Positionens värde måste vara 1–4 eller tomt."));

            var validPartyIds = (await db.Parties.Select(p => p.Id).ToListAsync()).ToHashSet();
            var byParty = (await db.PartyPositions.Where(p => p.QuestionId == id).ToListAsync())
                .ToDictionary(p => p.PartyId);
            var now = DateTimeOffset.UtcNow;

            foreach (var pin in input.Positions)
            {
                if (!validPartyIds.Contains(pin.PartyId)) continue;
                var value = pin.Value is null ? (ScaleValue?)null : (ScaleValue)pin.Value.Value;

                if (byParty.TryGetValue(pin.PartyId, out var pos))
                {
                    pos.Value = value; pos.Motivation = pin.Motivation;
                    pos.SourceCitation = pin.SourceCitation; pos.SourceUrl = pin.SourceUrl; pos.UpdatedAt = now;
                }
                else
                {
                    db.PartyPositions.Add(new PartyPosition
                    {
                        QuestionId = id, PartyId = pin.PartyId, Value = value,
                        Motivation = pin.Motivation, SourceCitation = pin.SourceCitation,
                        SourceUrl = pin.SourceUrl, UpdatedAt = now,
                    });
                }
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // Aggregat med spärr för små tal (k-anonymitet): fördelningar under detta antal döljs.
    private const int MinSample = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void MapStats(RouteGroupBuilder admin)
    {
        // Volym-/aktivitetsmått för genomförda kompasser. Returnerar medvetet bara antal och
        // tidsstämplar (+ radens Id som nyckel) – aldrig ShareToken, ResultJson eller partidata.
        admin.MapGet("/quiz/stats", async (AppDbContext db) =>
        {
            var now = DateTimeOffset.UtcNow;
            var sessions = db.QuizSessions;
            var latest = await sessions
                .OrderByDescending(s => s.CreatedAt)
                .Take(20)
                .Select(s => new QuizSessionSummaryDto(s.Id, s.CreatedAt))
                .ToListAsync();
            return Results.Ok(new QuizStatsDto(
                Total: await sessions.CountAsync(),
                Last24h: await sessions.CountAsync(s => s.CreatedAt >= now.AddHours(-24)),
                Last7d: await sessions.CountAsync(s => s.CreatedAt >= now.AddDays(-7)),
                Latest: latest));
        });

        // Aggregerad svarsfördelning per fråga. Ren GROUP BY över answers – inga per-person-rader.
        admin.MapGet("/quiz/answer-stats", async (AppDbContext db) =>
        {
            var sessions = await db.QuizSessions.CountAsync();

            // null Value = hoppat över. Gruppera per fråga + svarsvärde.
            var dist = await db.Answers
                .GroupBy(a => new { a.QuestionId, a.Value })
                .Select(g => new { g.Key.QuestionId, g.Key.Value, Count = g.Count() })
                .ToListAsync();
            var important = (await db.Answers.Where(a => a.IsImportant)
                    .GroupBy(a => a.QuestionId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync())
                .ToDictionary(x => x.Id, x => x.Count);

            var byQuestion = dist.GroupBy(d => d.QuestionId).ToDictionary(g => g.Key, g => g.ToList());

            var questions = await db.Questions.Include(q => q.Category)
                .OrderBy(q => q.Category!.DisplayOrder).ThenBy(q => q.DisplayOrder)
                .Select(q => new { q.Id, q.Text, Category = q.Category!.Name }).ToListAsync();

            var rows = new List<QuestionAnswerStatsDto>();
            foreach (var q in questions)
            {
                if (!byQuestion.TryGetValue(q.Id, out var cells)) continue; // bara frågor med svar
                int C(ScaleValue v) => cells.FirstOrDefault(c => c.Value == v)?.Count ?? 0;
                var skipped = cells.FirstOrDefault(c => c.Value == null)?.Count ?? 0;
                var sd = C(ScaleValue.StronglyDisagree);
                var pd = C(ScaleValue.PartlyDisagree);
                var pa = C(ScaleValue.PartlyAgree);
                var sa = C(ScaleValue.StronglyAgree);
                var answered = sd + pd + pa + sa;
                var total = answered + skipped;
                rows.Add(total < MinSample
                    ? new(q.Id, q.Category, q.Text, total, 0, 0, 0, 0, 0, 0, 0, Suppressed: true)
                    : new(q.Id, q.Category, q.Text, total, answered, skipped,
                        important.GetValueOrDefault(q.Id), sd, pd, pa, sa, Suppressed: false));
            }
            return Results.Ok(new AnswerStatsDto(sessions, rows));
        });

        // Aggregerad fördelning av bästa partimatchning. Läser ur det frysta result_json,
        // returnerar bara antal per parti – ingen koppling till enskilda sessioner.
        admin.MapGet("/quiz/party-stats", async (AppDbContext db) =>
        {
            var jsons = await db.QuizSessions.Select(s => s.ResultJson).ToListAsync();

            var counts = new Dictionary<string, int>();
            int tied = 0, sessions = 0;
            foreach (var json in jsons)
            {
                var doc = JsonSerializer.Deserialize<ResultDocument>(json, JsonOptions);
                var ranked = doc?.Overall.Where(o => o.AgreementPct is not null)
                    .OrderByDescending(o => o.AgreementPct).ToList();
                if (ranked is null || ranked.Count == 0) continue;
                sessions++;
                var top = ranked[0].AgreementPct!.Value;
                var leaders = ranked.Where(o => o.AgreementPct == top).Select(o => o.PartyCode).ToList();
                if (leaders.Count > 1) { tied++; continue; } // oavgjort räknas inte mot ett enskilt parti
                counts[leaders[0]] = counts.GetValueOrDefault(leaders[0]) + 1;
            }
            var slices = sessions < MinSample
                ? new List<PartyMatchSliceDto>()
                : counts.Select(kv => new PartyMatchSliceDto(kv.Key, kv.Value))
                    .OrderByDescending(s => s.Count).ToList();
            return Results.Ok(new PartyMatchStatsDto(sessions, tied, slices));
        });
    }

    // --- Hjälpare ---

    private static async Task<IResult?> SaveOrConflict(AppDbContext db, string conflictMessage)
    {
        try
        {
            await db.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = conflictMessage });
        }
    }

    private static Dictionary<string, string[]> Field(string name, string error) => new() { [name] = [error] };

    private static AdminCategoryDto Map(Category c) =>
        new(c.Id, c.Slug, c.Name, c.Description, c.Icon, c.DisplayOrder);

    private static AdminPartyDto Map(Party p, bool hasLogo) =>
        new(p.Id, p.Code, p.Name, p.FullName, p.ShortDescription, p.Color, p.DisplayOrder, p.IsActive, hasLogo);

    private static AdminQuestionDto Map(Question q) =>
        new(q.Id, q.ExternalKey, q.Text, q.Explanation, q.ExplanationSourceUrl, q.CategoryId,
            q.Category!.Slug, q.Category!.Name, q.DisplayOrder, q.Tier, q.IsActive);
}

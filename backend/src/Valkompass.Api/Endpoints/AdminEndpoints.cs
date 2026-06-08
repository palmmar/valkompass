using Microsoft.EntityFrameworkCore;
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
                .Select(p => Map(p)).ToListAsync()));

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
            return conflict ?? Results.Created($"/api/admin/parties/{entity.Id}", Map(entity));
        });

        admin.MapPut("/parties/{id:int}", async (int id, PartyInput input, AppDbContext db) =>
        {
            var entity = await db.Parties.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Code = input.Code; entity.Name = input.Name; entity.FullName = input.FullName;
            entity.ShortDescription = input.ShortDescription; entity.Color = input.Color;
            entity.DisplayOrder = input.DisplayOrder; entity.IsActive = input.IsActive;
            var conflict = await SaveOrConflict(db, "Ett parti med samma kod finns redan.");
            return conflict ?? Results.Ok(Map(entity));
        });

        admin.MapDelete("/parties/{id:int}", async (int id, AppDbContext db) =>
        {
            var entity = await db.Parties.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Parties.Remove(entity);
            return await SaveOrConflict(db, "Partiet kan inte tas bort.") ?? Results.NoContent();
        }).RequireAuthorization("AdminOnly");
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

            var now = DateTimeOffset.UtcNow;
            var entity = new Question
            {
                ExternalKey = input.ExternalKey, Text = input.Text, Explanation = input.Explanation,
                ExplanationSourceUrl = input.ExplanationSourceUrl,
                CategoryId = input.CategoryId, DisplayOrder = input.DisplayOrder, IsActive = input.IsActive,
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

            entity.ExternalKey = input.ExternalKey; entity.Text = input.Text; entity.Explanation = input.Explanation;
            entity.ExplanationSourceUrl = input.ExplanationSourceUrl;
            entity.CategoryId = input.CategoryId; entity.DisplayOrder = input.DisplayOrder; entity.IsActive = input.IsActive;
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

    private static AdminPartyDto Map(Party p) =>
        new(p.Id, p.Code, p.Name, p.FullName, p.ShortDescription, p.Color, p.DisplayOrder, p.IsActive);

    private static AdminQuestionDto Map(Question q) =>
        new(q.Id, q.ExternalKey, q.Text, q.Explanation, q.ExplanationSourceUrl, q.CategoryId,
            q.Category!.Slug, q.Category!.Name, q.DisplayOrder, q.IsActive);
}

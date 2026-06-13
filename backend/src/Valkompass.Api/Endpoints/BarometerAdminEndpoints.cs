using Microsoft.EntityFrameworkCore;
using Valkompass.Application.Dtos;
using Valkompass.Domain.Entities;
using Valkompass.Infrastructure.Persistence;

namespace Valkompass.Api.Endpoints;

/// <summary>
/// Admin-CRUD för valbarometern (institut + mätningar) bakom befintlig Admin/Editor cookie-auth.
/// Manuell backstop för det automatiska ingest-flödet. Helt åtskilt från quiz-/resultat-admin.
/// </summary>
public static class BarometerAdminEndpoints
{
    public static IEndpointRouteBuilder MapBarometerAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/barometer").WithTags("Admin")
            .RequireAuthorization("EditorOrAdmin");

        MapPollsters(admin);
        MapPolls(admin);
        return app;
    }

    private static void MapPollsters(RouteGroupBuilder admin)
    {
        admin.MapGet("/pollsters", async (AppDbContext db) =>
            Results.Ok(await db.Pollsters.OrderBy(p => p.Code)
                .Select(p => new AdminPollsterDto(
                    p.Id, p.Code, p.DisplayName, p.Method, p.Commissioner, p.Polls.Count))
                .ToListAsync()));

        admin.MapPost("/pollsters", async (PollsterInput input, AppDbContext db) =>
        {
            var entity = new Pollster
            {
                Code = input.Code, DisplayName = input.DisplayName,
                Method = input.Method, Commissioner = input.Commissioner, UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.Pollsters.Add(entity);
            return await SaveOrConflict(db, "Ett institut med samma kod finns redan.")
                ?? Results.Created($"/api/admin/barometer/pollsters/{entity.Id}", MapPollster(entity, 0));
        });

        admin.MapPut("/pollsters/{id:int}", async (int id, PollsterInput input, AppDbContext db) =>
        {
            var entity = await db.Pollsters.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Code = input.Code; entity.DisplayName = input.DisplayName;
            entity.Method = input.Method; entity.Commissioner = input.Commissioner;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            var conflict = await SaveOrConflict(db, "Ett institut med samma kod finns redan.");
            if (conflict is not null) return conflict;
            var count = await db.Polls.CountAsync(p => p.PollsterId == id);
            return Results.Ok(MapPollster(entity, count));
        });

        admin.MapDelete("/pollsters/{id:int}", async (int id, AppDbContext db) =>
        {
            var entity = await db.Pollsters.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Pollsters.Remove(entity);
            return await SaveOrConflict(db, "Institutet har mätningar och kan inte tas bort.")
                ?? Results.NoContent();
        }).RequireAuthorization("AdminOnly");
    }

    private static void MapPolls(RouteGroupBuilder admin)
    {
        admin.MapGet("/polls", async (string? pollster, AppDbContext db) =>
        {
            var order = await PartyOrderAsync(db);
            var query = db.Polls.Include(p => p.Pollster).Include(p => p.Results).AsNoTracking();
            if (pollster is { Length: > 0 }) query = query.Where(p => p.Pollster!.Code == pollster);
            var polls = await query.OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id).ToListAsync();
            return Results.Ok(polls.Select(p => MapPoll(p, order)).ToList());
        });

        admin.MapGet("/polls/{id:int}", async (int id, AppDbContext db) =>
        {
            var order = await PartyOrderAsync(db);
            var poll = await db.Polls.Include(p => p.Pollster).Include(p => p.Results).AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
            return poll is null ? Results.NotFound() : Results.Ok(MapPoll(poll, order));
        });

        admin.MapPost("/polls", async (PollInput input, AppDbContext db) =>
        {
            var pollster = await db.Pollsters.FirstOrDefaultAsync(p => p.Code == input.PollsterCode);
            if (pollster is null) return Results.ValidationProblem(Field("pollsterCode", "Okänt institut."));
            if (Validate(input) is { } badValue) return badValue;

            var entity = new Poll
            {
                ExternalKey = input.ExternalKey, PollsterId = pollster.Id,
                FieldStart = input.FieldStart, FieldEnd = input.FieldEnd, PublishedAt = input.PublishedAt,
                SampleSize = input.SampleSize, SourceUrl = input.SourceUrl,
                SourceCitation = input.SourceCitation, UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.Polls.Add(entity);
            var conflict = await SaveOrConflict(db, "En mätning med samma externalKey finns redan.");
            if (conflict is not null) return conflict;

            await UpsertResultsAsync(db, entity.Id, input.Results);
            await db.SaveChangesAsync();
            return Results.Created($"/api/admin/barometer/polls/{entity.Id}", await ReloadAsync(db, entity.Id));
        });

        admin.MapPut("/polls/{id:int}", async (int id, PollInput input, AppDbContext db) =>
        {
            var entity = await db.Polls.FindAsync(id);
            if (entity is null) return Results.NotFound();
            var pollster = await db.Pollsters.FirstOrDefaultAsync(p => p.Code == input.PollsterCode);
            if (pollster is null) return Results.ValidationProblem(Field("pollsterCode", "Okänt institut."));
            if (Validate(input) is { } badValue) return badValue;

            entity.ExternalKey = input.ExternalKey; entity.PollsterId = pollster.Id;
            entity.FieldStart = input.FieldStart; entity.FieldEnd = input.FieldEnd; entity.PublishedAt = input.PublishedAt;
            entity.SampleSize = input.SampleSize; entity.SourceUrl = input.SourceUrl;
            entity.SourceCitation = input.SourceCitation; entity.UpdatedAt = DateTimeOffset.UtcNow;
            var conflict = await SaveOrConflict(db, "En mätning med samma externalKey finns redan.");
            if (conflict is not null) return conflict;

            await UpsertResultsAsync(db, id, input.Results);
            await db.SaveChangesAsync();
            return Results.Ok(await ReloadAsync(db, id));
        });

        admin.MapDelete("/polls/{id:int}", async (int id, AppDbContext db) =>
        {
            var entity = await db.Polls.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Polls.Remove(entity); // PollResults raderas via cascade
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");
    }

    // --- Hjälpare ---

    private static IResult? Validate(PollInput input)
    {
        foreach (var r in input.Results)
            if (r.Value is < 0 or > 100)
                return Results.ValidationProblem(Field("value", "Stöd i procent måste vara 0–100 eller tomt (redovisas ej)."));
        return null;
    }

    private static async Task UpsertResultsAsync(AppDbContext db, int pollId, IReadOnlyList<PollResultInput> inputs)
    {
        var partyIdByCode = await db.Parties.ToDictionaryAsync(p => p.Code, p => p.Id);
        var existing = (await db.PollResults.Where(r => r.PollId == pollId).ToListAsync())
            .ToDictionary(r => r.PartyId);

        foreach (var r in inputs)
        {
            if (!partyIdByCode.TryGetValue(r.PartyCode, out var partyId)) continue;
            if (existing.TryGetValue(partyId, out var entity))
            {
                entity.Value = r.Value; entity.MarginOfError = r.MarginOfError;
            }
            else
            {
                db.PollResults.Add(new PollResult
                {
                    PollId = pollId, PartyId = partyId, Value = r.Value, MarginOfError = r.MarginOfError,
                });
            }
        }
    }

    private static async Task<AdminPollDto> ReloadAsync(AppDbContext db, int id)
    {
        var order = await PartyOrderAsync(db);
        var poll = await db.Polls.Include(p => p.Pollster).Include(p => p.Results).AsNoTracking()
            .FirstAsync(p => p.Id == id);
        return MapPoll(poll, order);
    }

    private static async Task<Dictionary<int, (string Code, int Order)>> PartyOrderAsync(AppDbContext db) =>
        await db.Parties.AsNoTracking().ToDictionaryAsync(p => p.Id, p => (p.Code, p.DisplayOrder));

    private static AdminPollsterDto MapPollster(Pollster p, int pollCount) =>
        new(p.Id, p.Code, p.DisplayName, p.Method, p.Commissioner, pollCount);

    private static AdminPollDto MapPoll(Poll p, IReadOnlyDictionary<int, (string Code, int Order)> order) =>
        new(p.Id, p.ExternalKey, p.Pollster!.Code, p.Pollster.DisplayName,
            p.FieldStart, p.FieldEnd, p.PublishedAt, p.SampleSize, p.SourceUrl, p.SourceCitation,
            p.Results.Where(r => order.ContainsKey(r.PartyId)).OrderBy(r => order[r.PartyId].Order)
                .Select(r => new AdminPollResultDto(order[r.PartyId].Code, r.Value, r.MarginOfError)).ToList());

    private static async Task<IResult?> SaveOrConflict(AppDbContext db, string conflictMessage)
    {
        try { await db.SaveChangesAsync(); return null; }
        catch (DbUpdateException) { return Results.Conflict(new { message = conflictMessage }); }
    }

    private static Dictionary<string, string[]> Field(string name, string error) => new() { [name] = [error] };
}

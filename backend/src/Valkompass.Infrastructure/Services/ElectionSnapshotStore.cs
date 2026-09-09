using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Valkompass.Application.Contracts;
using Valkompass.Application.Election;
using Valkompass.Domain.Entities;
using Valkompass.Domain.Enums;
using Valkompass.Infrastructure.Persistence;

namespace Valkompass.Infrastructure.Services;

/// <inheritdoc />
public class ElectionSnapshotStore(AppDbContext db) : IElectionSnapshotStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> SaveAsync(ElectionSnapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var alreadyStored = await db.ElectionSnapshots
            .AnyAsync(e => e.Stage == snapshot.Source.Stage && e.Checksum == snapshot.Source.Checksum, ct);
        if (alreadyStored)
        {
            return false;
        }

        db.ElectionSnapshots.Add(new ElectionSnapshotRecord
        {
            Stage = snapshot.Source.Stage,
            Checksum = snapshot.Source.Checksum,
            IsTest = snapshot.Source.IsTest,
            SourceUpdatedAt = snapshot.Source.UpdatedAt,
            IngestedAt = snapshot.Source.IngestedAt,
            DistrictsReported = snapshot.Reporting.DistrictsReported,
            DistrictsTotal = snapshot.Reporting.DistrictsTotal,
            Payload = JsonSerializer.Serialize(snapshot, SerializerOptions),
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException) when (await IsDuplicateAsync(snapshot, ct))
        {
            // Två samtidiga importförsök hann skriva samma checksumma. Det unika indexet är
            // det som avgör, och resultatet är detsamma som om vi hoppat över filen.
            return false;
        }
    }

    public async Task<ElectionSnapshot?> GetLatestAsync(
        CountingStage stage,
        bool includeTestData,
        CancellationToken ct = default)
    {
        var payload = await db.ElectionSnapshots
            .AsNoTracking()
            .Where(e => e.Stage == stage)
            .Where(e => includeTestData || !e.IsTest)
            .OrderByDescending(e => e.IngestedAt)
            .ThenByDescending(e => e.Id)
            .Select(e => e.Payload)
            .FirstOrDefaultAsync(ct);

        return payload is null
            ? null
            : JsonSerializer.Deserialize<ElectionSnapshot>(payload, SerializerOptions);
    }

    public async Task<string?> GetLatestChecksumAsync(CountingStage stage, CancellationToken ct = default) =>
        await db.ElectionSnapshots
            .AsNoTracking()
            .Where(e => e.Stage == stage)
            .OrderByDescending(e => e.IngestedAt)
            .ThenByDescending(e => e.Id)
            .Select(e => e.Checksum)
            .FirstOrDefaultAsync(ct);

    private async Task<bool> IsDuplicateAsync(ElectionSnapshot snapshot, CancellationToken ct)
    {
        // Rensa den misslyckade insättningen ur ChangeTracker innan vi frågar igen.
        db.ChangeTracker.Clear();
        return await db.ElectionSnapshots
            .AnyAsync(e => e.Stage == snapshot.Source.Stage && e.Checksum == snapshot.Source.Checksum, ct);
    }
}

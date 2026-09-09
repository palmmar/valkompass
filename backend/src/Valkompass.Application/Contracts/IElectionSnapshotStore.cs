using Valkompass.Application.Election;
using Valkompass.Application.Election.Nowcast;
using Valkompass.Domain.Enums;

namespace Valkompass.Application.Contracts;

/// <summary>
/// Lagrar och läser tillbaka importerade rösträkningssnapshots. Bytet av "nuvarande" snapshot
/// sker genom att en ny rad skrivs klart – en misslyckad import lämnar därmed alltid
/// föregående snapshot orörd.
/// </summary>
public interface IElectionSnapshotStore
{
    /// <summary>
    /// Sparar en snapshot. Är checksumman redan sparad för samma räkningstillfälle görs
    /// ingenting och metoden returnerar false – importen är idempotent.
    /// </summary>
    Task<bool> SaveAsync(ElectionSnapshot snapshot, NowcastResult? forecast, CancellationToken ct = default);

    /// <summary>
    /// Senast importerade snapshot, eller null om ingen finns. <paramref name="includeTestData"/>
    /// måste vara sant för att genrepsdata ska returneras.
    /// </summary>
    Task<StoredElectionSnapshot?> GetLatestAsync(
        CountingStage stage,
        bool includeTestData,
        CancellationToken ct = default);

    /// <summary>
    /// Checksumman för senast sparade snapshot, eller null. Används för att avgöra om en
    /// oförändrad fil kan hoppas över utan nedladdning.
    /// </summary>
    Task<string?> GetLatestChecksumAsync(CountingStage stage, CancellationToken ct = default);
}

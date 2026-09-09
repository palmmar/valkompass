using Valkompass.Application.Dtos;

namespace Valkompass.Application.Contracts;

/// <summary>Bygger valvakans publika läge från senast importerade snapshot.</summary>
public interface IElectionLiveService
{
    Task<ElectionLiveResponse> GetLiveAsync(CancellationToken ct = default);
}

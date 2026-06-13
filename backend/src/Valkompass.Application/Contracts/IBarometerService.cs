using Valkompass.Application.Dtos;

namespace Valkompass.Application.Contracts;

/// <summary>
/// Läsning av valbarometerns publika opinionsdata. Helt fristående från quiz/resultat:
/// ingen delad state, inga delningstoken, ingen användardata.
/// </summary>
public interface IBarometerService
{
    /// <summary>Listar enskilda mätningar, filtrerbart på institut och period (nyast först).</summary>
    Task<IReadOnlyList<BarometerPollDto>> GetPollsAsync(
        string? pollsterCode, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    /// <summary>Senaste läget: färskaste mätningen med förändring sedan senaste riksdagsval.</summary>
    Task<BarometerLatestDto?> GetLatestAsync(CancellationToken ct = default);

    /// <summary>
    /// Tidsserie per parti: enskilda mätpunkter, månadssnitt, glidande snitt (poll-of-polls)
    /// och faktiska valresultat som åtskild referens.
    /// </summary>
    Task<BarometerTimeseriesDto> GetTimeseriesAsync(
        DateOnly? from, DateOnly? to, CancellationToken ct = default);
}

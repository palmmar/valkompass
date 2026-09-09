using Valkompass.Application.Election.Nowcast;

namespace Valkompass.Application.Election;

/// <summary>
/// Ett sparat rösträkningsläge med den prognos som beräknades samtidigt. Fälten är skilda
/// hela vägen från lagring till API, så att ett räknat resultat kan serveras även när ingen
/// prognos finns.
/// </summary>
public sealed record StoredElectionSnapshot(ElectionSnapshot Result, NowcastResult? Forecast);

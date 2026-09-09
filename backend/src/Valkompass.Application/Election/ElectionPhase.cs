namespace Valkompass.Application.Election;

/// <summary>
/// Valvakans tillstånd. Poängen med en explicit fas är att datumkontroller inte ska spridas
/// ut i frontend – backend säger vad läget är, frontend bestämmer hur det ser ut (#85).
/// </summary>
/// <remarks>
/// Faserna kombinerar tid och data: klockan avgör bara om vallokalerna öppnat eller stängt.
/// Därefter är det rösträkningens faktiska status som styr. Att klockan passerat 20:00 betyder
/// alltså inte att det finns resultat att visa.
/// </remarks>
public enum ElectionPhase
{
    /// <summary>Före valdagen.</summary>
    PreElection,

    /// <summary>Valdagen, innan vallokalerna stänger.</summary>
    ElectionDay,

    /// <summary>Vallokalerna har stängt, men inga resultat har publicerats än.</summary>
    WaitingForResults,

    /// <summary>Preliminär rösträkning pågår.</summary>
    Live,

    /// <summary>
    /// Alla valdistrikt är preliminärt räknade. Räkningen är inte klar – onsdagens
    /// uppsamlingsräkning av sent inkomna röster återstår.
    /// </summary>
    PreliminaryPaused,

    /// <summary>Länsstyrelsernas slutliga rösträkning pågår.</summary>
    FinalCounting,

    /// <summary>Slutlig rösträkning klar.</summary>
    Finished,
}

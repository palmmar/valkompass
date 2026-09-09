namespace Valkompass.Application.Election.Nowcast;

/// <summary>
/// Modellens parametrar. Utbrutna hit för att kunna varieras i backtestet – särskilt
/// <see cref="SystematicSwingSigma"/>, som är det som avgör om 90 %-intervallen faktiskt
/// håller 90 %.
/// </summary>
public sealed class NowcastOptions
{
    /// <summary>
    /// Hur mycket lokalt underlag som krävs innan den lokala swingen får full vikt, mätt i
    /// 2022-röster. Ett område med lika mycket underlag som konstanten hamnar halvvägs mellan
    /// sin egen och den överliggande nivåns swing. Hindrar att några få tidiga distrikt får
    /// orimligt genomslag.
    /// </summary>
    public double ShrinkageVotes { get; init; } = 10_000;

    /// <summary>
    /// Systematisk osäkerhet: att de distrikt som hunnit rapportera inte är representativa för
    /// dem som återstår. Skalas med kvarvarande andel av rösterna, så att den försvinner när
    /// allt är räknat. Uttryckt som andel (0,01 = en procentenhet vid noll täckning).
    /// </summary>
    public double SystematicSwingSigma { get; init; } = 0.012;

    /// <summary>Antal simuleringar. Fler ger jämnare intervall men kostar tid.</summary>
    public int Draws { get; init; } = 1000;

    /// <summary>Låst frö gör prognosen reproducerbar för samma indata.</summary>
    public int Seed { get; init; } = 20260913;

    /// <summary>
    /// Minsta andel av rösterna som måste vara räknade innan en prognos lämnas alls. Under
    /// den gränsen säger modellen hellre ingenting än något godtyckligt.
    /// </summary>
    public double MinimumCoverage { get; init; } = 0.02;

    /// <summary>Minsta antal jämförbara distrikt som måste ha rapporterat.</summary>
    public int MinimumComparableDistricts { get; init; } = 100;

    /// <summary>
    /// Hur nära rikets kända 2022-total summan av kommunernas 2022-röster måste ligga för att
    /// baselinen ska godtas som fast. Ligger den långt under är den räknad bara på det som
    /// hunnit rapporteras, och då håller inte modellens antagande.
    /// </summary>
    public double MinimumBaselineShare { get; init; } = 0.9;
}

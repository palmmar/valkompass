namespace Valkompass.Application.Election.Nowcast;

/// <summary>
/// Modellens parametrar. Utbrutna hit för att kunna varieras i backtestet – särskilt
/// <see cref="SwingSigma"/>, som är det som avgör om 90 %-intervallen faktiskt
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
    /// dem som återstår.
    ///
    /// Skalas med sqrt(p(1-p)), alltså som standardfelet för en andel. Backtestet visade att
    /// båda de enklare alternativen är fel: en additiv stöt lika stor för alla partier ger för
    /// snäva intervall för de stora (träff 81 %), medan en stöt proportionell mot partiets
    /// storlek gör dem för snäva för de små (träff 63 %). Mellan S på 30 procent och L på 4,5
    /// skiljer sqrt(p(1-p)) en faktor 2,2, mot 6,7 för proportionell skalning.
    ///
    /// Skalas dessutom med kvarvarande andel av rösterna, så att den försvinner när allt är
    /// räknat. Värdet är kalibrerat mot backtestet.
    /// </summary>
    public double SwingSigma { get; init; } = 0.04;

    /// <summary>
    /// Hur hårt geografisk skevhet vidgar intervallen. Om de distrikt som rapporterat är
    /// koncentrerade till några få län är swingen skattad på ett urval som inte liknar landet,
    /// och då ska osäkerheten växa därefter. Utan detta höll intervallen bara 22 procent vid
    /// län-för-län-rapportering; modellen visste helt enkelt inte att den satt på ett skevt
    /// underlag.
    /// </summary>
    public double SkewPenalty { get; init; } = 2.5;

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

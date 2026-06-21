using Valkompass.Domain.Enums;

namespace Valkompass.Application.Matching;

/// <summary>
/// Ren matchningslogik – inga beroenden mot databas eller ASP.NET. Helt enhetstestbar.
///
/// Modell: skalvärden 1–4 mappas till centrerade värden (-1.5, -0.5, +0.5, +1.5).
/// Per fråga och parti, om BÅDE användaren svarat (ej hoppat över) OCH partiet har en
/// icke-null position: agreement = 1 − |användare − parti| / 3. "Extra viktig" ger dubbel
/// vikt. Total och per kategori = viktat medelvärde över jämförbara frågor. Saknas underlag
/// → null (inte 0 %).
///
/// I <c>binary</c>-läge (det förenklade swajp-testet) snäpps både användarens och partiets
/// värden till helt-ändarna innan överensstämmelsen beräknas (delvis → helt: 1/2 → 1, 3/4 → 4),
/// så att varje fråga blir antingen 100 % eller 0 % träff. Det påverkar bara beräkningen –
/// partiets faktiska position lagras oförändrad i fråga-för-fråga-vyn.
/// </summary>
public static class MatchCalculator
{
    public const double MaxDistance = 3.0;
    public const double ImportantWeight = 2.0;
    public const double NormalWeight = 1.0;

    /// <summary>Mappar 1–4 till centrerat värde (-1.5 … +1.5).</summary>
    private static double Centered(ScaleValue v) => (int)v - 2.5;

    /// <summary>Snäpper ett delvis-värde till närmaste helt-ände (1/2 → 1, 3/4 → 4).</summary>
    private static ScaleValue Snap(ScaleValue v) =>
        v <= ScaleValue.PartlyDisagree ? ScaleValue.StronglyDisagree : ScaleValue.StronglyAgree;

    private static double Weight(bool isImportant) => isImportant ? ImportantWeight : NormalWeight;

    public static MatchResult Calculate(
        IReadOnlyCollection<MatchAnswer> answers,
        IReadOnlyCollection<MatchQuestion> questions,
        IReadOnlyCollection<MatchParty> parties,
        IReadOnlyCollection<MatchPartyPosition> positions,
        bool binary = false)
    {
        var categoryByQuestion = questions.ToDictionary(q => q.QuestionId, q => q.CategoryId);
        var displayOrder = parties.ToDictionary(p => p.PartyId, p => p.DisplayOrder);

        // (partyId, questionId) -> partiets värde (kan vara null = oklar).
        var positionLookup = positions
            .GroupBy(p => (p.PartyId, p.QuestionId))
            .ToDictionary(g => g.Key, g => g.First().Value);

        // Bara frågor som användaren faktiskt besvarat bidrar till matchningen.
        var answeredQuestions = answers
            .Where(a => a is { IsSkipped: false, Value: not null })
            .ToList();

        // --- Total och per kategori, per parti ---
        var overall = new List<PartyScore>();
        var categoryAcc = new Dictionary<int, List<PartyScore>>();

        foreach (var party in parties)
        {
            double weightedSum = 0, weightTotal = 0;
            var compared = 0;
            // category -> (viktad summa, total vikt, antal)
            var perCategory = new Dictionary<int, (double Sum, double Weight, int Count)>();

            foreach (var answer in answeredQuestions)
            {
                if (!positionLookup.TryGetValue((party.PartyId, answer.QuestionId), out var partyValue) || partyValue is null)
                    continue; // partiet har ingen jämförbar position på frågan

                var agreement = AgreementFraction(answer.Value!.Value, partyValue.Value, binary);
                var weight = Weight(answer.IsImportant);

                weightedSum += agreement * weight;
                weightTotal += weight;
                compared++;

                var categoryId = categoryByQuestion[answer.QuestionId];
                var c = perCategory.GetValueOrDefault(categoryId);
                perCategory[categoryId] = (c.Sum + agreement * weight, c.Weight + weight, c.Count + 1);
            }

            var overallPct = weightTotal > 0 ? weightedSum / weightTotal * 100.0 : (double?)null;
            overall.Add(new PartyScore(party.PartyId, party.Code, overallPct, compared));

            foreach (var (categoryId, c) in perCategory)
            {
                var pct = c.Weight > 0 ? c.Sum / c.Weight * 100.0 : (double?)null;
                categoryAcc.TryAdd(categoryId, []);
                categoryAcc[categoryId].Add(new PartyScore(party.PartyId, party.Code, pct, c.Count));
            }
        }

        var byCategory = categoryAcc
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new CategoryScore(kvp.Key, Rank(kvp.Value, displayOrder)))
            .ToList();

        // --- Fråga-för-fråga-jämförelse (inkluderar även överhoppade frågor) ---
        var byQuestion = answers
            .Select(answer =>
            {
                var userValue = answer.IsSkipped ? null : answer.Value;
                var partyComparisons = parties.Select(party =>
                {
                    var partyValue = positionLookup.GetValueOrDefault((party.PartyId, answer.QuestionId));
                    double? agreement = userValue is not null && partyValue is not null
                        ? AgreementFraction(userValue.Value, partyValue.Value, binary) * 100.0
                        : null;
                    return new QuestionPartyComparison(party.PartyId, party.Code, partyValue, agreement);
                }).ToList();

                return new QuestionComparison(
                    answer.QuestionId, userValue, answer.IsSkipped, answer.IsImportant, partyComparisons);
            })
            .ToList();

        return new MatchResult(Rank(overall, displayOrder), byCategory, byQuestion);
    }

    /// <summary>
    /// Överensstämmelse 0–1 mellan två skalvärden (1 = identiskt, 0 = motsatt). I
    /// <paramref name="binary"/>-läge snäpps båda värdena till helt-ändarna först, så att
    /// resultatet alltid blir 1 (samma sida) eller 0 (motsatt sida).
    /// </summary>
    private static double AgreementFraction(ScaleValue user, ScaleValue party, bool binary)
    {
        if (binary)
        {
            user = Snap(user);
            party = Snap(party);
        }
        var distance = Math.Abs(Centered(user) - Centered(party));
        return 1.0 - distance / MaxDistance;
    }

    /// <summary>Rankar fallande på överensstämmelse; tie-break på antal jämförda, sedan visningsordning.</summary>
    private static List<PartyScore> Rank(IEnumerable<PartyScore> scores, IReadOnlyDictionary<int, int> displayOrder) =>
        scores
            .OrderByDescending(s => s.AgreementPct.HasValue) // partier utan underlag hamnar sist
            .ThenByDescending(s => s.AgreementPct ?? 0)
            .ThenByDescending(s => s.ComparedQuestionCount)
            .ThenBy(s => displayOrder.GetValueOrDefault(s.PartyId))
            .ToList();
}

using Valkompass.Application.Matching;
using Valkompass.Domain.Enums;

namespace Valkompass.UnitTests;

public class MatchCalculatorTests
{
    // Bekvämlighetsalias för skalvärden
    private const ScaleValue Disagree = ScaleValue.StronglyDisagree;   // 1
    private const ScaleValue PartlyDis = ScaleValue.PartlyDisagree;    // 2
    private const ScaleValue PartlyAgr = ScaleValue.PartlyAgree;       // 3
    private const ScaleValue Agree = ScaleValue.StronglyAgree;         // 4

    private static MatchParty Party(int id, string code, int order = 0) => new(id, code, order);
    private static MatchQuestion Q(int id, int cat = 1) => new(id, cat);
    private static MatchPartyPosition Pos(int partyId, int qId, ScaleValue? v) => new(partyId, qId, v);
    private static MatchAnswer Ans(int qId, ScaleValue? v, bool skipped = false, bool important = false)
        => new(qId, v, skipped, important);

    private static PartyScore OverallFor(MatchResult r, string code) =>
        r.Overall.Single(p => p.PartyCode == code);

    [Fact]
    public void IdenticalAnswer_Gives100Percent()
    {
        var result = MatchCalculator.Calculate(
            answers: [Ans(1, Agree)],
            questions: [Q(1)],
            parties: [Party(1, "S")],
            positions: [Pos(1, 1, Agree)]);

        var s = OverallFor(result, "S");
        Assert.Equal(100.0, s.AgreementPct!.Value, 5);
        Assert.Equal(1, s.ComparedQuestionCount);
    }

    [Fact]
    public void OppositeAnswer_Gives0Percent()
    {
        var result = MatchCalculator.Calculate(
            [Ans(1, Agree)], [Q(1)], [Party(1, "S")], [Pos(1, 1, Disagree)]);

        Assert.Equal(0.0, OverallFor(result, "S").AgreementPct!.Value, 5);
    }

    [Fact]
    public void OneStepApart_Gives66Point67Percent()
    {
        // användare 4, parti 3 → avstånd 1.0 av 3 → 66.67 %
        var result = MatchCalculator.Calculate(
            [Ans(1, Agree)], [Q(1)], [Party(1, "S")], [Pos(1, 1, PartlyAgr)]);

        Assert.Equal(66.67, OverallFor(result, "S").AgreementPct!.Value, 2);
    }

    [Fact]
    public void SkippedQuestion_IsExcludedForAllParties()
    {
        // Q1 besvaras (perfekt match), Q2 hoppas över (skulle annars ge 0 %).
        var result = MatchCalculator.Calculate(
            answers: [Ans(1, Agree), Ans(2, null, skipped: true)],
            questions: [Q(1), Q(2)],
            parties: [Party(1, "S")],
            positions: [Pos(1, 1, Agree), Pos(1, 2, Disagree)]);

        var s = OverallFor(result, "S");
        Assert.Equal(100.0, s.AgreementPct!.Value, 5);
        Assert.Equal(1, s.ComparedQuestionCount); // bara Q1 räknas

        // Den överhoppade frågan finns med i jämförelsen men utan överensstämmelse.
        var skipped = result.ByQuestion.Single(q => q.QuestionId == 2);
        Assert.True(skipped.Skipped);
        Assert.Null(skipped.UserValue);
        Assert.Null(skipped.Parties.Single().AgreementPct);
    }

    [Fact]
    public void UnclearPartyPosition_IsExcludedForThatPartyOnly()
    {
        var result = MatchCalculator.Calculate(
            answers: [Ans(1, Agree)],
            questions: [Q(1)],
            parties: [Party(1, "S"), Party(2, "M")],
            positions: [Pos(1, 1, Agree), Pos(2, 1, null)]); // M oklar

        var s = OverallFor(result, "S");
        var m = OverallFor(result, "M");
        Assert.Equal(100.0, s.AgreementPct!.Value, 5);
        Assert.Equal(1, s.ComparedQuestionCount);
        Assert.Null(m.AgreementPct);          // inget underlag
        Assert.Equal(0, m.ComparedQuestionCount);
    }

    [Fact]
    public void MissingPartyPositionRow_BehavesLikeUnclear()
    {
        // M saknar helt en rad för frågan → ska exkluderas precis som null-position.
        var result = MatchCalculator.Calculate(
            [Ans(1, Agree)], [Q(1)],
            [Party(1, "S"), Party(2, "M")],
            [Pos(1, 1, Agree)]);

        Assert.Null(OverallFor(result, "M").AgreementPct);
        Assert.Equal(0, OverallFor(result, "M").ComparedQuestionCount);
    }

    [Fact]
    public void ImportantFlag_DoublesWeight()
    {
        // Q1: oense (0 %), Q2: ense (100 %).
        var questions = new[] { Q(1), Q(2) };
        var parties = new[] { Party(1, "S") };
        var positions = new[] { Pos(1, 1, Disagree), Pos(1, 2, Agree) };

        // Baslinje: lika vikt → (0 + 1) / 2 = 50 %.
        var baseline = MatchCalculator.Calculate(
            [Ans(1, Agree), Ans(2, Agree)], questions, parties, positions);
        Assert.Equal(50.0, OverallFor(baseline, "S").AgreementPct!.Value, 5);

        // Q1 (oense) markeras extra viktig → (0·2 + 1·1) / (2 + 1) = 33.33 %.
        var weighted = MatchCalculator.Calculate(
            [Ans(1, Agree, important: true), Ans(2, Agree)], questions, parties, positions);
        Assert.Equal(33.33, OverallFor(weighted, "S").AgreementPct!.Value, 2);
    }

    [Fact]
    public void EmptyComparableSet_YieldsNullOverall()
    {
        // Användaren hoppar över allt → inget parti har underlag.
        var result = MatchCalculator.Calculate(
            [Ans(1, null, skipped: true)],
            [Q(1)],
            [Party(1, "S")],
            [Pos(1, 1, Agree)]);

        Assert.Null(OverallFor(result, "S").AgreementPct);
        Assert.Equal(0, OverallFor(result, "S").ComparedQuestionCount);
    }

    [Fact]
    public void PerCategory_IsComputedIndependently()
    {
        // Cat 1: perfekt match (100 %). Cat 2: motsats (0 %). Total = 50 %.
        var result = MatchCalculator.Calculate(
            answers: [Ans(1, Agree), Ans(2, Agree)],
            questions: [Q(1, cat: 1), Q(2, cat: 2)],
            parties: [Party(1, "S")],
            positions: [Pos(1, 1, Agree), Pos(1, 2, Disagree)]);

        Assert.Equal(50.0, OverallFor(result, "S").AgreementPct!.Value, 5);

        var cat1 = result.ByCategory.Single(c => c.CategoryId == 1).Parties.Single();
        var cat2 = result.ByCategory.Single(c => c.CategoryId == 2).Parties.Single();
        Assert.Equal(100.0, cat1.AgreementPct!.Value, 5);
        Assert.Equal(0.0, cat2.AgreementPct!.Value, 5);
    }

    [Fact]
    public void Ranking_OrdersByAgreementDescending()
    {
        // S perfekt, M motsats → S ska rankas först.
        var result = MatchCalculator.Calculate(
            answers: [Ans(1, Agree)],
            questions: [Q(1)],
            parties: [Party(1, "S", order: 1), Party(2, "M", order: 2)],
            positions: [Pos(1, 1, Agree), Pos(2, 1, Disagree)]);

        Assert.Equal("S", result.Overall[0].PartyCode);
        Assert.Equal("M", result.Overall[1].PartyCode);
    }

    [Fact]
    public void Ranking_TieBreaksByDisplayOrder()
    {
        // Båda perfekt match (lika %, lika antal) → visningsordning avgör.
        var result = MatchCalculator.Calculate(
            answers: [Ans(1, Agree)],
            questions: [Q(1)],
            parties: [Party(2, "M", order: 5), Party(1, "S", order: 1)],
            positions: [Pos(1, 1, Agree), Pos(2, 1, Agree)]);

        Assert.Equal("S", result.Overall[0].PartyCode); // order 1 före order 5
        Assert.Equal("M", result.Overall[1].PartyCode);
    }

    [Fact]
    public void PartiesWithoutData_AreRankedLast()
    {
        var result = MatchCalculator.Calculate(
            answers: [Ans(1, Agree)],
            questions: [Q(1)],
            parties: [Party(1, "S", order: 1), Party(2, "M", order: 2)],
            positions: [Pos(2, 1, PartlyAgr)]); // bara M har data

        Assert.Equal("M", result.Overall[0].PartyCode);
        Assert.Null(result.Overall[1].AgreementPct); // S utan underlag hamnar sist
        Assert.Equal("S", result.Overall[1].PartyCode);
    }

    [Fact]
    public void Binary_PartlyAgreeCountsAsFullyAgree_Gives100Percent()
    {
        // Användare 4, parti 3 (delvis med). Normalt 66.67 %, men binärt snäpps 3 → 4 → 100 %.
        var result = MatchCalculator.Calculate(
            [Ans(1, Agree)], [Q(1)], [Party(1, "S")], [Pos(1, 1, PartlyAgr)], binary: true);

        Assert.Equal(100.0, OverallFor(result, "S").AgreementPct!.Value, 5);
    }

    [Fact]
    public void Binary_PartlyDisagreeCountsAsFullyDisagree_Gives0Percent()
    {
        // Användare 4, parti 2 (delvis emot). Normalt 33.33 %, men binärt snäpps 2 → 1 → 0 %.
        var result = MatchCalculator.Calculate(
            [Ans(1, Agree)], [Q(1)], [Party(1, "S")], [Pos(1, 1, PartlyDis)], binary: true);

        Assert.Equal(0.0, OverallFor(result, "S").AgreementPct!.Value, 5);
    }

    [Fact]
    public void Binary_DisagreeVsPartlyDisagree_Gives100Percent()
    {
        // Användare 1, parti 2 (delvis emot) → båda snäpps till samma sida (1) → 100 %.
        var result = MatchCalculator.Calculate(
            [Ans(1, Disagree)], [Q(1)], [Party(1, "S")], [Pos(1, 1, PartlyDis)], binary: true);

        Assert.Equal(100.0, OverallFor(result, "S").AgreementPct!.Value, 5);
    }

    [Fact]
    public void Binary_StoresOriginalPartyValue_NotSnapped()
    {
        // Träffen blir binär (100 %) men partiets visade position ska förbli den faktiska (3).
        var result = MatchCalculator.Calculate(
            [Ans(1, Agree)], [Q(1)], [Party(1, "S")], [Pos(1, 1, PartlyAgr)], binary: true);

        var qc = result.ByQuestion.Single().Parties.Single();
        Assert.Equal(PartlyAgr, qc.PartyValue);          // oförändrad position
        Assert.Equal(100.0, qc.AgreementPct!.Value, 5);  // men binär träff
    }

    [Fact]
    public void Binary_FalseLeavesPartialScoringUnchanged()
    {
        // Regression: utan binary-flaggan ska delvis-positioner ge mellanvärden som vanligt.
        var result = MatchCalculator.Calculate(
            [Ans(1, Agree)], [Q(1)], [Party(1, "S")], [Pos(1, 1, PartlyAgr)]);

        Assert.Equal(66.67, OverallFor(result, "S").AgreementPct!.Value, 2);
    }

    [Fact]
    public void ByQuestion_CarriesUserValueAndImportantFlag()
    {
        var result = MatchCalculator.Calculate(
            answers: [Ans(1, PartlyDis, important: true)],
            questions: [Q(1)],
            parties: [Party(1, "S")],
            positions: [Pos(1, 1, PartlyDis)]);

        var qc = result.ByQuestion.Single();
        Assert.Equal(PartlyDis, qc.UserValue);
        Assert.True(qc.IsImportant);
        Assert.False(qc.Skipped);
        Assert.Equal(100.0, qc.Parties.Single().AgreementPct!.Value, 5);
        Assert.Equal(PartlyDis, qc.Parties.Single().PartyValue);
    }
}

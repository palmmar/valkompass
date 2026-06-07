using Valkompass.Domain.Enums;

namespace Valkompass.Application.Matching;

// --- Indata (rena värden, frikopplade från EF-entiteter) ---

/// <summary>Ett användarsvar. <c>Value</c> är null när frågan hoppats över.</summary>
public sealed record MatchAnswer(int QuestionId, ScaleValue? Value, bool IsSkipped, bool IsImportant);

/// <summary>Frågans tillhörighet till ett politikområde.</summary>
public sealed record MatchQuestion(int QuestionId, int CategoryId);

/// <summary>Ett parti med visningsordning (för rankning/tie-break).</summary>
public sealed record MatchParty(int PartyId, string Code, int DisplayOrder);

/// <summary>Ett partis position på en fråga. <c>Value</c> null = oklar → exkluderas för partiet.</summary>
public sealed record MatchPartyPosition(int PartyId, int QuestionId, ScaleValue? Value);

// --- Utdata ---

/// <summary>Ett partis överensstämmelse (total eller inom ett område). Null när underlag saknas.</summary>
public sealed record PartyScore(int PartyId, string PartyCode, double? AgreementPct, int ComparedQuestionCount);

/// <summary>Partiernas överensstämmelse inom ett politikområde, rankad.</summary>
public sealed record CategoryScore(int CategoryId, IReadOnlyList<PartyScore> Parties);

/// <summary>Ett partis svar och överensstämmelse på en enskild fråga.</summary>
public sealed record QuestionPartyComparison(int PartyId, string PartyCode, ScaleValue? PartyValue, double? AgreementPct);

/// <summary>Fråga-för-fråga-jämförelse: användarens svar mot varje parti.</summary>
public sealed record QuestionComparison(
    int QuestionId,
    ScaleValue? UserValue,
    bool Skipped,
    bool IsImportant,
    IReadOnlyList<QuestionPartyComparison> Parties);

/// <summary>Det fullständiga matchningsresultatet.</summary>
public sealed record MatchResult(
    IReadOnlyList<PartyScore> Overall,
    IReadOnlyList<CategoryScore> ByCategory,
    IReadOnlyList<QuestionComparison> ByQuestion);

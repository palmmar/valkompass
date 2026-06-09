#!/usr/bin/env python3
"""
Tvådimensionell positionering av partierna (och användaren) på:
  - en ekonomisk vänster–höger-axel
  - en GAL–TAN-axel (värdeaxel)

Metoden ("samma linjal"):
  1. Varje påstående taggas i axes.json med en riktningsvikt per axel (-1/0/+1),
     där tecknet pekar mot axelns HÖGA ände (höger resp. TAN).
  2. Partiets råa axelvärde = medelvärdet av (vikt * centrerat svar) över de
     taggade frågor där partiet har en känd position. Centrering: 1..4 -> -1.5..+1.5
     (samma som MatchCalculator). Råvärdet ligger i [-1.5, +1.5] och skalas om till 0..10.
  3. Användaren placeras med EXAKT samma formel utifrån sina egna svar -> samma karta.

Oberoende/validering:
  Den data-härledda axeln jämförs mot Chapel Hill Expert Survey (ches_reference.json),
  en oberoende akademisk skattning. Hög rangkorrelation (Spearman) = vår axel mäter
  samma konstrukt. En enkel OLS-kalibrering (lutning + intercept utifrån de 8 partierna)
  låter oss dessutom uttrycka användarens värde på CHES-skalan.

CHES används ALDRIG som indata till partikoordinaterna — bara som facit i efterhand.

Körning:
  python3 compute_axes.py                 # partitabell + validering, skriver axes_output.json
  python3 compute_axes.py --wave 2019     # validera mot CHES 2019 i stället för 2024
  python3 compute_axes.py svar.json       # placera även en användare; svar.json = {"questionKey": 1..4, ...}
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
CONTENT = os.path.normpath(os.path.join(
    HERE, "..", "..", "src", "Valkompass.Infrastructure", "Seed", "Content"))
PARTIES = ["V", "S", "MP", "C", "L", "KD", "M", "SD"]
AXES = ("econ", "galtan")
AXIS_LABEL = {"econ": "Ekonomi (0=vänster, 10=höger)",
              "galtan": "GAL-TAN (0=GAL, 10=TAN)"}
CHES_VAR = {"econ": "lrecon", "galtan": "galtan"}


def load_json(*parts):
    with open(os.path.join(*parts), encoding="utf-8") as f:
        return json.load(f)


def centered(value):
    """1..4 -> -1.5..+1.5 (samma mappning som MatchCalculator.Centered)."""
    return value - 2.5


def to_ten(raw):
    """Råvärde i [-1.5, +1.5] -> 0..10."""
    return (raw / 1.5) * 5.0 + 5.0


def score_axis(answers_by_key, weights):
    """answers_by_key: {questionKey: value 1..4}. weights: {questionKey: vikt}.
    Returnerar (ten 0..10, antal frågor som bidrog) eller (None, 0)."""
    total, n = 0.0, 0
    for key, w in weights.items():
        if w == 0:
            continue
        v = answers_by_key.get(key)
        if v is None:
            continue
        total += w * centered(v)
        n += 1
    if n == 0:
        return None, 0
    return to_ten(total / n), n


# ---- statistik (stdlib, inga beroenden) ----
def _ranks(xs):
    order = sorted(range(len(xs)), key=lambda i: xs[i])
    ranks = [0.0] * len(xs)
    i = 0
    while i < len(xs):
        j = i
        while j + 1 < len(xs) and xs[order[j + 1]] == xs[order[i]]:
            j += 1
        avg = (i + j) / 2.0 + 1.0
        for k in range(i, j + 1):
            ranks[order[k]] = avg
        i = j + 1
    return ranks


def pearson(xs, ys):
    n = len(xs)
    mx, my = sum(xs) / n, sum(ys) / n
    sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    sxx = sum((x - mx) ** 2 for x in xs)
    syy = sum((y - my) ** 2 for y in ys)
    if sxx == 0 or syy == 0:
        return float("nan")
    return sxy / (sxx ** 0.5 * syy ** 0.5)


def spearman(xs, ys):
    return pearson(_ranks(xs), _ranks(ys))


def ols(xs, ys):
    """Returnerar (lutning, intercept, R^2) för ys ~ a*xs + b."""
    n = len(xs)
    mx, my = sum(xs) / n, sum(ys) / n
    sxx = sum((x - mx) ** 2 for x in xs)
    sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    slope = sxy / sxx
    intercept = my - slope * mx
    r = pearson(xs, ys)
    return slope, intercept, r * r


def main():
    args = [a for a in sys.argv[1:]]
    wave = "2024"
    if "--wave" in args:
        i = args.index("--wave")
        wave = args[i + 1]
        del args[i:i + 2]
    user_path = args[0] if args else None

    axes = load_json(HERE, "axes.json")
    ches = load_json(HERE, "ches_reference.json")
    positions = load_json(CONTENT, "positions.json")

    weights = {a: {} for a in AXES}
    for q in axes["questions"]:
        for a in AXES:
            weights[a][q["questionKey"]] = q[a]

    counts = {a: sum(1 for w in weights[a].values() if w != 0) for a in AXES}
    print(f"Taggade frågor: ekonomi={counts['econ']}, galtan={counts['galtan']}, "
          f"totalt {len(axes['questions'])} (resten avsiktligt uteslutna).\n")

    # Partiernas svar (1..4) per fråga.
    party_answers = {p: {} for p in PARTIES}
    for pos in positions:
        if pos["value"] is not None:
            party_answers[pos["partyCode"]][pos["questionKey"]] = pos["value"]

    # Data-härledda koordinater.
    data = {a: {} for a in AXES}
    contributed = {a: {} for a in AXES}
    for p in PARTIES:
        for a in AXES:
            ten, n = score_axis(party_answers[p], weights[a])
            data[a][p] = ten
            contributed[a][p] = n

    ches_wave = ches["waves"][wave]

    # ---- Validering mot CHES + kalibrering ----
    calib = {}
    print(f"== Validering mot CHES {wave} (oberoende facit) ==")
    for a in AXES:
        xs = [data[a][p] for p in PARTIES]
        ys = [ches_wave[p][CHES_VAR[a]] for p in PARTIES]
        rho, r = spearman(xs, ys), pearson(xs, ys)
        slope, intercept, r2 = ols(xs, ys)
        calib[a] = (slope, intercept)
        print(f"  {AXIS_LABEL[a]}")
        print(f"    Spearman (rangordning) = {rho:+.3f}   Pearson = {r:+.3f}   "
              f"kalibrering: CHES ≈ {slope:.2f}·data {intercept:+.2f}  (R²={r2:.2f})")
    print()

    # ---- Partitabell ----
    print("== Partikoordinater ==")
    print(f"{'':3}  {'EKONOMI (0 v - 10 h)':>26}   {'GAL-TAN (0 GAL - 10 TAN)':>30}")
    print(f"{'':3}  {'data':>6} {'→CHES':>6} {'CHES':>6} {'n':>3}   "
          f"{'data':>6} {'→CHES':>6} {'CHES':>6} {'n':>3}")
    for p in PARTIES:
        row = f"{p:3} "
        for a in AXES:
            s, i = calib[a]
            d = data[a][p]
            cal = s * d + i
            ref = ches_wave[p][CHES_VAR[a]]
            row += f"  {d:6.1f} {cal:6.1f} {ref:6.1f} {contributed[a][p]:3d} "
        print(row)
    print()

    # ---- Användarplacering ----
    user_out = None
    if user_path:
        user_answers = load_json(os.getcwd(), user_path) if not os.path.isabs(user_path) \
            else load_json(user_path)
        user_answers = {k: int(v) for k, v in user_answers.items()}
        print("== Din placering ==")
        user_out = {}
        for a in AXES:
            ten, n = score_axis(user_answers, weights[a])
            if ten is None:
                print(f"  {AXIS_LABEL[a]}: inga svar på axelns frågor")
                continue
            s, i = calib[a]
            cal = s * ten + i
            user_out[a] = {"data": round(ten, 2), "chesCalibrated": round(cal, 2), "n": n}
            print(f"  {AXIS_LABEL[a]}: data={ten:.1f}  på CHES-skala≈{cal:.1f}  ({n} frågor)")
        print()

    # ---- Artefakt för ev. framtida appbruk ----
    out = {
        "method": "signed-mean of centered 1..4 answers per axis, rescaled to 0..10",
        "chesWave": wave,
        "axisLabels": AXIS_LABEL,
        "calibrationToChes": {a: {"slope": round(calib[a][0], 4),
                                  "intercept": round(calib[a][1], 4)} for a in AXES},
        "parties": {
            p: {a: {"data": round(data[a][p], 2),
                    "chesCalibrated": round(calib[a][0] * data[a][p] + calib[a][1], 2),
                    "ches": ches_wave[p][CHES_VAR[a]],
                    "n": contributed[a][p]} for a in AXES}
            for p in PARTIES
        },
    }
    if user_out is not None:
        out["user"] = user_out
    out_path = os.path.join(HERE, "axes_output.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"Skrev {out_path}")


if __name__ == "__main__":
    main()

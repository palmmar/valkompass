#!/usr/bin/env python3
"""
Genererar polls.json för valbarometern (fristående opinionsdata – ingen koppling till quiz).

Primärflöde: MansMeg/SwedishPolls (Data/Polls.csv) – CC0-data, hela det svenska
opinionsfältet sedan 2015. Officiellt komplement: SCB:s Partisympatiundersökning (PSU)
via PXWeb-API:t (tabell ME0201A/Vid10) som ger den enda myndighetsproducerade mätningen
och dessutom en officiell felmarginal (±).

Proveniens-nyans: institutet (Novus, Demoskop, SCB …) behandlas som *primärkälla*;
SwedishPolls/SCB-API:t är *leveranskanal*. Per mätning lagras institut, fältperiod,
antal svar (n) och käll-URL.

Datakvirks som hanteras (dokumenterade i SwedishPolls README):
  * Ipsos avrundar ibland så summan blir 101 → normaliseras med faktor 100/summan.
  * Sentio rapporterar två tal; CSV:n innehåller redan partipreferenserna (ett tal per
    parti), så kvirket är upplöst i källan.
  * Synovate→Ipsos, Temo→Sifo m.fl. konsolideras via kolumnen `house` (inte `Company`),
    så samma institut inte dyker upp som flera serier.

null, aldrig 0: "NA" i CSV:n och partier under redovisningsgräns lagras som null.

Kör:  python3 gen_polls.py            # hämtar källor, skriver Seed/Content/polls.json
      python3 gen_polls.py --no-scb  # hoppar över SCB-API:t (bara SwedishPolls)
"""
import argparse
import csv
import datetime as dt
import io
import json
import os
import urllib.request

SWEDISH_POLLS_URL = "https://raw.githubusercontent.com/MansMeg/SwedishPolls/master/Data/Polls.csv"
SWEDISH_POLLS_REPO = "https://github.com/MansMeg/SwedishPolls"
SCB_API = "https://api.scb.se/OV0104/v1/doris/sv/ssd/START/ME/ME0201/ME0201A/Vid10"
SCB_TABLE_URL = "https://www.statistikdatabasen.scb.se/pxweb/sv/ssd/START__ME__ME0201__ME0201A/Vid10/"

# Inkludera flera mandatperioder (data börjar 2015 i SwedishPolls).
SINCE_YEAR = 2015

# De åtta riksdagspartierna. CSV-kolumn → partikod (FI utelämnas, ej riksdagsparti).
CSV_PARTY_COLS = {"M": "M", "L": "L", "C": "C", "KD": "KD", "S": "S", "V": "V", "MP": "MP", "SD": "SD"}

# house (harmoniserat institut i SwedishPolls) → vår institutkod + visningsnamn.
HOUSE = {
    "Sifo": ("sifo", "Sifo"),
    "Ipsos": ("ipsos", "Ipsos"),
    "Demoskop": ("demoskop", "Demoskop"),
    "Novus": ("novus", "Novus"),
    "Skop": ("skop", "Skop"),
    "Sentio": ("sentio", "Sentio"),
    "SCB": ("scb", "SCB"),
    "YouGov": ("yougov", "YouGov"),
    "United Minds": ("united-minds", "United Minds"),
    "Inizio": ("inizio", "Inizio"),
    "Indikator": ("indikator", "Indikator"),
    "SVT": ("svt", "SVT (VALU)"),
    "Infostat": ("infostat", "Infostat"),
    "TV4": ("tv4", "TV4"),
}

# SCB PXWeb: partikod i tabellen → vår partikod (block/historiska partier utelämnas).
SCB_PARTY = {"c": "C", "l": "L", "m": "M", "kd": "KD", "s": "S", "v": "V", "mp": "MP", "SD": "SD"}

# Faktiska riksdagsvalresultat (Valmyndigheten). Hålls åtskilt från opinionsmätningar:
# valresultat ≠ opinion. Egen "institutkod" val-YYYY, exkluderas ur opinionssnitten i API:t.
ELECTIONS = {
    "2014-09-14": {"V": 5.72, "S": 31.01, "MP": 6.89, "C": 6.11, "L": 5.42, "KD": 4.57, "M": 23.33, "SD": 12.86},
    "2018-09-09": {"V": 8.00, "S": 28.26, "MP": 4.41, "C": 8.61, "L": 5.49, "KD": 6.32, "M": 19.84, "SD": 17.53},
    "2022-09-11": {"V": 6.75, "S": 30.33, "MP": 5.08, "C": 6.71, "L": 4.61, "KD": 5.34, "M": 19.10, "SD": 20.54},
}
ELECTION_SOURCE = "https://www.val.se/valresultat/riksdag-region-och-kommun.html"


def fetch(url, data=None, timeout=60):
    headers = {"User-Agent": "valkompass-barometer/1.0 (+https://github.com/palmmar/valkompass)"}
    if data is not None:
        body = json.dumps(data).encode("utf-8")
        req = urllib.request.Request(url, data=body, headers={**headers, "Content-Type": "application/json"})
    else:
        req = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


def parse_date(s):
    s = (s or "").strip()
    if not s or s == "NA":
        return None
    try:
        return dt.date.fromisoformat(s)
    except ValueError:
        return None


def num(s):
    """CSV-cell → float, eller None för 'NA'/tom (null, aldrig 0)."""
    s = (s or "").strip()
    if not s or s == "NA":
        return None
    try:
        return float(s)
    except ValueError:
        return None


def normalize_ipsos_101(values):
    """Skala om en rad vars partisumma avrundats till ~101 (Ipsos-kvirket) med faktor 100/summan."""
    present = [v for v in values.values() if v is not None]
    total = sum(present)
    if 100.5 <= total <= 101.5:
        return {k: (round(v * 100.0 / total, 1) if v is not None else None) for k, v in values.items()}
    return values


def load_swedish_polls(raw):
    reader = csv.DictReader(io.StringIO(raw.decode("utf-8")))
    polls = []
    seen = set()
    for row in reader:
        house = (row.get("house") or "").strip()
        if house not in HOUSE:
            continue
        published = parse_date(row.get("PublDate"))
        field_start = parse_date(row.get("collectPeriodFrom"))
        field_end = parse_date(row.get("collectPeriodTo"))
        # Tidsaxel = publiceringsdatum; fall tillbaka på fältperiodens slut om det saknas.
        published = published or field_end or field_start
        if published is None or published.year < SINCE_YEAR:
            continue

        values = {code: num(row.get(col)) for col, code in CSV_PARTY_COLS.items()}
        values = normalize_ipsos_101(values)
        if not any(v is not None for v in values.values()):
            continue

        code, display = HOUSE[house]
        # Stabil naturlig nyckel; lägg till suffix vid krock (samma institut samma dag).
        base = f"{code}-{published.isoformat()}"
        key, i = base, 2
        while key in seen:
            key, i = f"{base}-{i}", i + 1
        seen.add(key)

        n = row.get("n")
        sample = None
        if n and n.strip() != "NA":
            try:
                sample = int(float(n))
            except ValueError:
                sample = None

        if code == "scb":
            citation = "SCB, Partisympatiundersökningen (PSU)"
            source_url = SCB_TABLE_URL
            method = "Slumpmässigt urval (PSU)"
        else:
            citation = f"{display}, fältperiod {field_start or '?'}–{field_end or '?'} (via SwedishPolls)"
            source_url = SWEDISH_POLLS_REPO
            method = None

        polls.append({
            "externalKey": key,
            "pollsterCode": code,
            "pollsterName": display,
            "method": method,
            "fieldStart": field_start.isoformat() if field_start else None,
            "fieldEnd": field_end.isoformat() if field_end else None,
            "publishedAt": published.isoformat(),
            "sampleSize": sample,
            "sourceUrl": source_url,
            "sourceCitation": citation,
            "results": {k: v for k, v in values.items()},
            "_ym": (published.year, published.month),
        })
    return polls


def parse_jsonstat2(doc):
    """json-stat2 → {(contents, parti, tid): value}. Generisk uppslagning via dimensionsindex."""
    ids = doc["id"]
    size = doc["size"]
    values = doc["value"]
    index = {d: doc["dimension"][d]["category"]["index"] for d in ids}
    # Radmajor-strides.
    strides = [1] * len(ids)
    for i in range(len(ids) - 2, -1, -1):
        strides[i] = strides[i + 1] * size[i + 1]

    pos = {d: index[d] for d in ids}
    out = {}
    # Iterera över alla kombinationer via varje dimensions koder.
    contents_dim = "ContentsCode"
    parti_dim = "Parti"
    tid_dim = "Tid"
    for c_code, c_i in pos[contents_dim].items():
        for p_code, p_i in pos[parti_dim].items():
            for t_code, t_i in pos[tid_dim].items():
                offset = 0
                ordinals = {contents_dim: c_i, parti_dim: p_i, tid_dim: t_i}
                for d_i, d in enumerate(ids):
                    offset += ordinals[d] * strides[d_i]
                v = values[offset] if offset < len(values) else None
                out[(c_code, p_code, t_code)] = v
    return out


def fetch_scb_margins():
    """Hämtar officiell felmarginal (±) per parti och mätmånad från SCB PXWeb.

    Returnerar {(år, månad): {partikod: moe}} eller {} vid fel (graceful fallback)."""
    query = {
        "query": [
            {"code": "Parti", "selection": {"filter": "item", "values": list(SCB_PARTY.keys())}},
            {"code": "ContentsCode", "selection": {"filter": "item", "values": ["ME0201B4"]}},
            {"code": "Tid", "selection": {"filter": "all", "values": ["*"]}},
        ],
        "response": {"format": "json-stat2"},
    }
    doc = json.loads(fetch(SCB_API, data=query))
    cells = parse_jsonstat2(doc)
    margins = {}
    for (_contents, parti, tid), v in cells.items():
        if v is None or parti not in SCB_PARTY:
            continue
        try:
            year, month = int(tid[:4]), int(tid[5:7])
        except (ValueError, IndexError):
            continue
        if year < SINCE_YEAR:
            continue
        margins.setdefault((year, month), {})[SCB_PARTY[parti]] = round(float(v), 1)
    return margins


def attach_scb_margins(polls, margins):
    """Fäster officiell SCB-felmarginal på matchande SCB-mätning (per mätmånad)."""
    attached = 0
    for p in polls:
        if p["pollsterCode"] != "scb":
            continue
        # Mätmånad ≈ fältperiodens slut; fall tillbaka på publiceringsmånad.
        ym = p["_ym"]
        if p["fieldEnd"]:
            fe = dt.date.fromisoformat(p["fieldEnd"])
            ym = (fe.year, fe.month)
        moe = margins.get(ym) or margins.get(p["_ym"])
        if moe:
            p["marginOfError"] = {k: moe[k] for k in p["results"] if k in moe}
            p["sourceCitation"] = "SCB, Partisympatiundersökningen (PSU), felmarginal via SCB:s öppna API"
            attached += 1
    return attached


def election_polls():
    polls = []
    for date_str, results in ELECTIONS.items():
        d = dt.date.fromisoformat(date_str)
        polls.append({
            "externalKey": f"val-{d.year}",
            "pollsterCode": f"val-{d.year}",
            "pollsterName": f"Riksdagsval {d.year}",
            "method": "Faktiskt valresultat",
            "fieldStart": None,
            "fieldEnd": None,
            "publishedAt": d.isoformat(),
            "sampleSize": None,
            "sourceUrl": ELECTION_SOURCE,
            "sourceCitation": f"Valmyndigheten, riksdagsvalet {d.year} (officiellt slutresultat)",
            "results": dict(results),
        })
    return polls


def build_pollsters(polls):
    by_code = {}
    for p in polls:
        code = p["pollsterCode"]
        if code not in by_code:
            by_code[code] = {
                "code": code,
                "displayName": p["pollsterName"],
                "method": p.get("method"),
                "commissioner": None,
            }
    return [by_code[c] for c in sorted(by_code)]


def main():
    ap = argparse.ArgumentParser(description="Genererar polls.json för valbarometern.")
    ap.add_argument("--no-scb", action="store_true", help="Hoppa över SCB:s PXWeb-API (bara SwedishPolls).")
    args = ap.parse_args()

    here = os.path.dirname(os.path.abspath(__file__))
    content_dir = os.path.normpath(os.path.join(
        here, "..", "src", "Valkompass.Infrastructure", "Seed", "Content"))

    print(f"Hämtar SwedishPolls … {SWEDISH_POLLS_URL}")
    polls = load_swedish_polls(fetch(SWEDISH_POLLS_URL))
    print(f"  {len(polls)} mätningar (sedan {SINCE_YEAR}).")

    if not args.no_scb:
        try:
            print("Hämtar officiell felmarginal från SCB PXWeb (PSU, ME0201A/Vid10) …")
            margins = fetch_scb_margins()
            attached = attach_scb_margins(polls, margins)
            print(f"  felmarginal fäst på {attached} SCB-mätningar ({len(margins)} mätmånader).")
        except Exception as e:  # noqa: BLE001 – graceful fallback, SCB-data finns ändå via CSV.
            print(f"  VARNING: kunde inte hämta SCB PXWeb ({e}). Fortsätter utan officiell felmarginal.")

    polls.extend(election_polls())
    polls.sort(key=lambda p: (p["publishedAt"], p["pollsterCode"]))

    pollsters = build_pollsters(polls)
    for p in polls:
        p.pop("_ym", None)
        p.pop("pollsterName", None)
        p.pop("method", None)

    out = {"pollsters": pollsters, "polls": polls}
    path = os.path.join(content_dir, "polls.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
        f.write("\n")

    nulls = sum(1 for p in polls for v in p["results"].values() if v is None)
    span = f'{polls[0]["publishedAt"]}–{polls[-1]["publishedAt"]}' if polls else "—"
    print(f"Skrev {len(polls)} mätningar / {len(pollsters)} institut ({span}) till {path}")
    print(f"  {nulls} null-celler (under redovisningsgräns / NA).")


if __name__ == "__main__":
    main()

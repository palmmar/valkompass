#!/usr/bin/env python3
"""
Slår ihop research-batchar (results/new75-batch*.json) in i sources.json och
explanations.json. Upsert på (questionKey, partyCode) respektive questionKey,
så omkörning är säker. Kör därefter ../gen_content.py för att regenerera seedfilerna.
"""
import glob
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save(path, data):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


def main():
    sources_path = os.path.join(HERE, "sources.json")
    expl_path = os.path.join(HERE, "explanations.json")
    sources = load(sources_path)
    explanations = load(expl_path)

    src_idx = {(s["questionKey"], s["partyCode"]): i for i, s in enumerate(sources)}
    expl_idx = {e["questionKey"]: i for i, e in enumerate(explanations)}

    batches = sorted(glob.glob(os.path.join(HERE, "results", "new75-batch*.json")))
    added_src = updated_src = added_expl = 0
    for path in batches:
        batch = load(path)
        for s in batch.get("sources", []):
            key = (s["questionKey"], s["partyCode"])
            if key in src_idx:
                sources[src_idx[key]] = s
                updated_src += 1
            else:
                src_idx[key] = len(sources)
                sources.append(s)
                added_src += 1
        for e in batch.get("explanations", []):
            if e["questionKey"] in expl_idx:
                explanations[expl_idx[e["questionKey"]]] = e
            else:
                expl_idx[e["questionKey"]] = len(explanations)
                explanations.append(e)
                added_expl += 1

    save(sources_path, sources)
    save(expl_path, explanations)
    print(f"Batches: {len(batches)}. Sources: +{added_src} new, {updated_src} updated "
          f"(totalt {len(sources)}). Explanations: +{added_expl} (totalt {len(explanations)}).")


if __name__ == "__main__":
    main()

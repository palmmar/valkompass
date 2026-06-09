// Tvådimensionell positionering: ekonomisk vänster–höger + GAL–TAN.
//
// Datadelen (PARTY_AXES, WEIGHTS, CALIBRATION) är härledd ur backend/tools/galtan
// (axes.json + CHES-validering, våg 2024). Se backend/tools/galtan/README.md för metod
// och förbehåll. Genereras om med `python3 compute_axes.py` om taggningen ändras.
//
// Partierna och användaren placeras med EXAKT samma formel ("samma linjal"):
//   råvärde = medel av (vikt · centrerat svar) över axelns besvarade frågor,
//   centrering 1..4 -> -1.5..+1.5, skalas till 0..10, kalibreras till CHES-skalan.

export type Axis = "econ" | "galtan";

export interface AxisPoint {
  econ: number;
  galtan: number;
}

export const AXIS_META = {
  econ: { label: "Ekonomi", low: "Vänster", high: "Höger" },
  galtan: { label: "GAL–TAN", low: "GAL", high: "TAN" },
} as const;

/** Partikoordinater på CHES-kalibrerad skala 0–10 (econ: 0 vänster … 10 höger; galtan: 0 GAL … 10 TAN). */
export const PARTY_AXES: Record<string, AxisPoint> = {
  V: { econ: 1.91, galtan: 1.84 },
  S: { econ: 3.7, galtan: 4.81 },
  MP: { econ: 3.22, galtan: 1.95 },
  C: { econ: 7.4, galtan: 3.05 },
  L: { econ: 7.64, galtan: 6.13 },
  KD: { econ: 7.4, galtan: 7.22 },
  M: { econ: 8.0, galtan: 7.11 },
  SD: { econ: 6.09, galtan: 7.66 },
};

/** Riktningsvikt per fråga. Tecken pekar mot axelns höga ände (höger / TAN). Nollor utelämnade. */
const WEIGHTS: Record<string, Partial<Record<Axis, number>>> = {
  "sjukvard-vinstforbud": { econ: -1 },
  "sjukvard-privat-vard": { econ: 1 },
  "sjukvard-mer-resurser": { econ: -1 },
  "sjukvard-tandvard": { econ: -1 },
  "sjukvard-privata-forsakringar": { econ: -1 },
  "ekonomi-hoginkomstskatt": { econ: -1 },
  "ekonomi-formogenhetsskatt": { econ: -1 },
  "ekonomi-sankt-skatt-arbete": { econ: 1 },
  "ekonomi-drivmedelsskatt": { econ: 1 },
  "skola-friskolor-vinst": { econ: -1 },
  "arbete-las": { econ: 1 },
  "arbete-akassa": { econ: -1 },
  "arbete-bidragstak": { econ: 1 },
  "arbete-arbetstid": { econ: -1 },
  "aldre-resurser": { econ: -1 },
  "aldre-pension": { econ: -1 },
  "aldre-privat-omsorg": { econ: -1 },
  "bostad-hyressattning": { econ: 1 },
  "bostad-subventioner": { econ: -1 },
  "lag-skarpta-straff": { galtan: 1 },
  "lag-fler-poliser": { galtan: 1 },
  "lag-visitationszoner": { galtan: 1 },
  "lag-anonyma-vittnen": { galtan: 1 },
  "lag-sankt-straffmyndighet": { galtan: 1 },
  "lag-kameraovervakning": { galtan: 1 },
  "migration-faerre-asyl": { galtan: 1 },
  "migration-medborgarskap": { galtan: 1 },
  "migration-arbetskraft": { galtan: 1 },
  "migration-atervandring": { galtan: 1 },
  "migration-sprakkrav-bidrag": { galtan: 1 },
  "migration-utvisning-vandel": { galtan: 1 },
  "ekonomi-bistand": { galtan: 1 },
  "klimat-karnkraft-utbyggnad": { galtan: 1 },
  "klimat-skarpta-mal": { galtan: -1 },
  "klimat-vindkraft": { galtan: -1 },
  "klimat-flygskatt": { galtan: -1 },
  "klimat-reduktionsplikt": { galtan: 1 },
  "klimat-prioritet": { galtan: -1 },
  "skola-mobilforbud": { galtan: 1 },
};

/** OLS-kalibrering data→CHES per axel (lutning, intercept). */
const CALIBRATION: Record<Axis, { slope: number; intercept: number }> = {
  econ: { slope: 0.6804, intercept: 1.7903 },
  galtan: { slope: 0.6587, intercept: 1.0762 },
};

/** Minsta antal besvarade axelfrågor för att en användarkoordinat ska visas. */
export const MIN_AXIS_ANSWERS = 3;

export interface UserAxisAnswer {
  externalKey: string;
  userValue: number | null;
  skipped: boolean;
}

export interface UserPlacement {
  econ: number | null;
  galtan: number | null;
  econN: number;
  galtanN: number;
}

const centered = (value: number) => value - 2.5;

function axisScore(answers: UserAxisAnswer[], axis: Axis): { value: number | null; n: number } {
  let total = 0;
  let n = 0;
  for (const a of answers) {
    if (a.skipped || a.userValue == null) continue;
    const w = WEIGHTS[a.externalKey]?.[axis] ?? 0;
    if (w === 0) continue;
    total += w * centered(a.userValue);
    n += 1;
  }
  if (n < MIN_AXIS_ANSWERS) return { value: null, n };
  const ten = (total / n / 1.5) * 5 + 5;
  const { slope, intercept } = CALIBRATION[axis];
  return { value: slope * ten + intercept, n };
}

/** Placerar användaren på samma 0–10-karta som partierna utifrån resultatets svar. */
export function placeUser(answers: UserAxisAnswer[]): UserPlacement {
  const econ = axisScore(answers, "econ");
  const galtan = axisScore(answers, "galtan");
  return { econ: econ.value, galtan: galtan.value, econN: econ.n, galtanN: galtan.n };
}

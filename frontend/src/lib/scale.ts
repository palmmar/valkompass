// Etiketter och hjälpfunktioner för den fyrgradiga skalan (1–4).

export interface ScaleOption {
  value: number;
  label: string;
  short: string;
}

export const SCALE_OPTIONS: ScaleOption[] = [
  { value: 4, label: "Håller helt med", short: "Håller med" },
  { value: 3, label: "Håller delvis med", short: "Delvis med" },
  { value: 2, label: "Håller delvis inte med", short: "Delvis emot" },
  { value: 1, label: "Håller inte med", short: "Emot" },
];

export function scaleLabel(value: number | null | undefined): string {
  if (value == null) return "Oklart";
  return SCALE_OPTIONS.find((o) => o.value === value)?.label ?? "Oklart";
}

export function scaleShort(value: number | null | undefined): string {
  if (value == null) return "Oklart";
  return SCALE_OPTIONS.find((o) => o.value === value)?.short ?? "Oklart";
}

/** Formaterar en procentandel för visning, eller "–" när underlag saknas. */
export function formatPct(pct: number | null | undefined): string {
  if (pct == null) return "–";
  return `${Math.round(pct)} %`;
}

/** Fallback-färg när ett parti saknar färg. */
export function partyColor(color: string | null | undefined): string {
  return color && /^#[0-9a-fA-F]{6}$/.test(color) ? color : "#6b7280";
}

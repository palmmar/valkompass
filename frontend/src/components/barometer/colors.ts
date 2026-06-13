// Små färghjälpare för barometern. Medvetet fristående från lib/scale.ts (quiz) så att
// barometern inte kopplas ihop med resultatflödet – samma regex-validering, egen kopia.

const FALLBACK = "#6b7280";

/** Giltig hex-färg eller en neutral fallback. */
export function partyColor(color: string | null | undefined): string {
  return color && /^#[0-9a-fA-F]{6}$/.test(color) ? color : FALLBACK;
}

/** Hex → rgba med given opacitet (för felmarginalband m.m.). */
export function withAlpha(color: string | null | undefined, alpha: number): string {
  const hex = partyColor(color).slice(1);
  const r = parseInt(hex.slice(0, 2), 16);
  const g = parseInt(hex.slice(2, 4), 16);
  const b = parseInt(hex.slice(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

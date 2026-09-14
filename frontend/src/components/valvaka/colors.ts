// Färghjälpare för valvakan. Egen kopia, av samma skäl som barometern har en: valvakan ska
// inte dra in barometerns modul och därmed koppla ihop rösträkningen med opinionsmätningar.

const FALLBACK = "#6b7280";

/** Giltig hex-färg eller en neutral fallback. */
export function partyColor(color: string | null | undefined): string {
  return color && /^#[0-9a-fA-F]{6}$/.test(color) ? color : FALLBACK;
}

/**
 * Svart eller vit text – den av de två som har högst kontrast mot bakgrunden. Partifärgerna
 * spänner från SD:s ljusa guld till KD:s marinblå, så en hårdkodad vit text hade blivit
 * oläslig på hälften av dem.
 */
export function readableTextColor(color: string | null | undefined): string {
  const hex = partyColor(color).slice(1);
  const channel = (i: number) => {
    const value = parseInt(hex.slice(i, i + 2), 16) / 255;
    return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  };
  // WCAG relativ luminans. Kontrasten mot vitt är (1.05)/(L+0.05), mot svart (L+0.05)/0.05;
  // de är lika vid L ≈ 0.179.
  const luminance = 0.2126 * channel(0) + 0.7152 * channel(2) + 0.0722 * channel(4);
  return luminance > 0.179 ? "#14130f" : "#ffffff";
}

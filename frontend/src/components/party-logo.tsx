"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";
import { partyColor } from "@/lib/scale";

interface PartyLogoProps {
  /** Partikod, t.ex. "S", "M", "SD". Avgör vilken logotyp som laddas. */
  code: string;
  /** Partiets färg (hex). Används för fallback-cirkeln. */
  color?: string | null;
  /** Storlek i pixlar (kvadratisk). */
  size?: number;
  className?: string;
}

/**
 * Visar ett partis logotyp från `/public/parties/<code>.svg` (gemener).
 * Saknas filen – eller misslyckas laddningen – visas en färgad cirkel med
 * partikoden som fallback, samma utseende som innan logotyper fanns.
 */
export function PartyLogo({ code, color, size = 40, className }: PartyLogoProps) {
  const [failed, setFailed] = useState(false);
  const box = { width: size, height: size };

  if (failed) {
    return (
      <span
        aria-label={`${code} logotyp`}
        className={cn(
          "inline-flex shrink-0 items-center justify-center rounded-full font-bold leading-none text-white",
          className,
        )}
        style={{ ...box, backgroundColor: partyColor(color), fontSize: Math.round(size * 0.38) }}
      >
        {code}
      </span>
    );
  }

  return (
    // Statiska SVG:er från /public – plain <img> räcker, ingen next/image-optimering behövs.
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={`/parties/${code.toLowerCase()}.svg`}
      alt={`${code} logotyp`}
      style={box}
      className={cn("shrink-0 object-contain", className)}
      onError={() => setFailed(true)}
    />
  );
}

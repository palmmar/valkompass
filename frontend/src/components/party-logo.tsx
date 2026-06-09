"use client";

import { useEffect, useRef, useState } from "react";
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
  const imgRef = useRef<HTMLImageElement>(null);
  const box = { width: size, height: size };

  // Vid SSR finns <img> redan i HTML:en, så bilden kan hinna faila (t.ex. 404) INNAN
  // React hydrerat och hängt på onError – då missas error-eventet och alt-texten fastnar.
  // decode() avvisas om bilden inte kan laddas (men löser ut för en giltig SVG oavsett
  // intrinsiska mått), så vi fångar även ett fel som hann ske före hydreringen.
  useEffect(() => {
    const img = imgRef.current;
    if (!img?.decode) return;
    img.decode().catch(() => setFailed(true));
  }, []);

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
      ref={imgRef}
      src={`/parties/${code.toLowerCase()}.svg`}
      alt={`${code} logotyp`}
      style={box}
      className={cn("shrink-0 object-contain", className)}
      onError={() => setFailed(true)}
    />
  );
}

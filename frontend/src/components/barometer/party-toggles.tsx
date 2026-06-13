"use client";

import type { BarometerParty } from "@/lib/barometer-api";
import { partyColor } from "./colors";
import { cn } from "@/lib/utils";

interface PartyTogglesProps {
  parties: BarometerParty[];
  visible: Set<string>;
  onToggle: (code: string) => void;
  onSetAll: (on: boolean) => void;
}

/**
 * Partitoggels: användaren väljer själv vilka partier som visas (ingen påtvingad
 * blockindelning – vill man jämföra ett block bygger man det själv genom att toggla).
 * Alla åtta partier behandlas lika.
 */
export function PartyToggles({ parties, visible, onToggle, onSetAll }: PartyTogglesProps) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      {parties.map((p) => {
        const on = visible.has(p.code);
        const color = partyColor(p.color);
        return (
          <button
            key={p.code}
            type="button"
            onClick={() => onToggle(p.code)}
            aria-pressed={on}
            className={cn(
              "inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-sm transition-colors",
              on ? "text-white" : "text-muted-foreground hover:text-foreground",
            )}
            style={
              on
                ? { backgroundColor: color, borderColor: color }
                : { borderColor: color, color }
            }
          >
            <span
              aria-hidden
              className="inline-block h-2.5 w-2.5 rounded-full"
              style={{ backgroundColor: on ? "white" : color }}
            />
            {p.code}
          </button>
        );
      })}
      <span className="ml-1 flex gap-2 text-xs text-muted-foreground">
        <button type="button" className="underline hover:text-foreground" onClick={() => onSetAll(true)}>
          Alla
        </button>
        <button type="button" className="underline hover:text-foreground" onClick={() => onSetAll(false)}>
          Inga
        </button>
      </span>
    </div>
  );
}

"use client";

import type { BarometerParty } from "@/lib/barometer-api";
import type { Group } from "./blocks";
import { partyColor } from "./colors";
import { cn } from "@/lib/utils";

interface BlockAssignmentProps {
  parties: BarometerParty[];
  assignment: Record<string, Group>;
  colorA: string;
  colorB: string;
  onChange: (code: string, group: Group) => void;
  onReset: () => void;
}

/**
 * Låter användaren placera varje parti i Block 1, utanför, eller Block 2. Default-placeringen
 * (S·V·MP·C mot M·KD·L·SD) är bara en utgångspunkt – grupperingen är fri och inte påtvingad.
 */
export function BlockAssignment({ parties, assignment, colorA, colorB, onChange, onReset }: BlockAssignmentProps) {
  const Segment = ({ code, group, color, label }: { code: string; group: Group; color: string | null; label: string }) => {
    const active = (assignment[code] ?? "none") === group;
    return (
      <button
        type="button"
        onClick={() => onChange(code, group)}
        aria-pressed={active}
        data-party={code}
        data-group={group}
        title={label}
        className={cn(
          "px-2.5 py-1 text-xs transition-colors",
          active ? "font-medium text-white" : "text-muted-foreground hover:text-foreground",
        )}
        style={active && color ? { backgroundColor: color } : undefined}
      >
        {label}
      </button>
    );
  };

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">Placera partierna i blocken:</p>
        <button type="button" onClick={onReset} className="text-xs text-muted-foreground underline hover:text-foreground">
          Återställ
        </button>
      </div>
      <div className="grid grid-cols-1 gap-1.5 sm:grid-cols-2">
        {parties.map((p) => (
          <div key={p.code} className="flex items-center gap-2">
            <span className="inline-block h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: partyColor(p.color) }} />
            <span className="w-9 shrink-0 text-sm font-medium">{p.code}</span>
            <div className="inline-flex divide-x overflow-hidden rounded-full border">
              <Segment code={p.code} group="a" color={colorA} label="Block 1" />
              <Segment code={p.code} group="none" color={null} label="Utanför" />
              <Segment code={p.code} group="b" color={colorB} label="Block 2" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

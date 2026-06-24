import Link from "next/link";

/** Litet kompass-/kartmärke: plotruta med kryss och en teal "du"-prick. */
function CompassMark() {
  return (
    <svg width="22" height="22" viewBox="0 0 22 22" aria-hidden className="shrink-0">
      <rect
        x="1.5"
        y="1.5"
        width="19"
        height="19"
        rx="4"
        fill="none"
        className="stroke-foreground/70"
        strokeWidth="1.5"
      />
      <g className="stroke-foreground/30" strokeWidth="1" strokeDasharray="2 2">
        <line x1="11" y1="3.5" x2="11" y2="18.5" />
        <line x1="3.5" y1="11" x2="18.5" y2="11" />
      </g>
      <circle cx="14.6" cy="7.4" r="2.6" className="fill-primary" />
    </svg>
  );
}

export function SiteHeader() {
  return (
    <header className="border-b">
      <div className="mx-auto flex h-14 max-w-5xl items-center justify-between px-4">
        <Link href="/" className="flex items-center gap-2.5">
          <CompassMark />
          <span className="font-heading text-lg font-semibold tracking-tight">
            Valkompass <span className="font-normal text-muted-foreground">2026</span>
          </span>
        </Link>
        <nav className="flex items-center gap-5 text-sm text-muted-foreground">
          <Link href="/barometer" className="transition-colors hover:text-foreground">
            Valbarometer
          </Link>
          <Link href="/quiz" className="transition-colors hover:text-foreground">
            Gör testet
          </Link>
        </nav>
      </div>
    </header>
  );
}

import Link from "next/link";

export function SiteHeader() {
  return (
    <header className="border-b">
      <div className="mx-auto flex h-14 max-w-5xl items-center justify-between px-4">
        <Link href="/" className="font-semibold tracking-tight">
          Valkompass <span className="text-muted-foreground">2026</span>
        </Link>
        <nav className="text-sm text-muted-foreground">
          <Link href="/quiz" className="hover:text-foreground">
            Gör testet
          </Link>
        </nav>
      </div>
    </header>
  );
}

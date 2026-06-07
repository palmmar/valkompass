import Link from "next/link";

export function SiteFooter() {
  return (
    <footer className="mt-16 border-t">
      <div className="mx-auto flex max-w-5xl flex-col gap-2 px-4 py-8 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
        <p>
          Valkompass 2026 – ett obundet verktyg. Resultatet är vägledande, inte en
          rekommendation.
        </p>
        <nav className="flex gap-4">
          <Link href="/om" className="hover:text-foreground">
            Om &amp; metod
          </Link>
          <Link href="/admin" className="hover:text-foreground">
            Admin
          </Link>
        </nav>
      </div>
    </footer>
  );
}

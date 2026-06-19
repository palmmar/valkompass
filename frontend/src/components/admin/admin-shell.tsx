"use client";

import { useEffect } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getMe, logout } from "@/lib/admin-api";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const NAV = [
  { href: "/admin", label: "Översikt" },
  { href: "/admin/questions", label: "Frågor" },
  { href: "/admin/categories", label: "Kategorier" },
  { href: "/admin/parties", label: "Partier" },
  { href: "/admin/statistik", label: "Statistik" },
];

export function AdminShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const queryClient = useQueryClient();
  const isLogin = pathname === "/admin/login";

  const me = useQuery({ queryKey: ["me"], queryFn: getMe, enabled: !isLogin });

  useEffect(() => {
    if (!isLogin && me.isError) router.replace("/admin/login");
  }, [isLogin, me.isError, router]);

  if (isLogin) return <>{children}</>;
  if (me.isLoading) return <div className="p-12 text-center text-muted-foreground">Laddar…</div>;
  if (me.isError || !me.data) return null;

  async function onLogout() {
    try {
      await logout();
    } finally {
      queryClient.removeQueries({ queryKey: ["me"] });
      router.replace("/admin/login");
    }
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-6">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3 border-b pb-4">
        <nav className="flex gap-4 text-sm">
          {NAV.map((n) => (
            <Link
              key={n.href}
              href={n.href}
              className={cn(
                "hover:text-foreground",
                pathname === n.href ? "font-medium text-foreground" : "text-muted-foreground",
              )}
            >
              {n.label}
            </Link>
          ))}
        </nav>
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <span>{me.data.email}</span>
          <Button variant="outline" size="sm" onClick={onLogout}>
            Logga ut
          </Button>
        </div>
      </div>
      {children}
    </div>
  );
}

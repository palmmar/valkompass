"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { listCategories, listParties, listQuestions } from "@/lib/admin-api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function AdminDashboard() {
  const questions = useQuery({ queryKey: ["questions"], queryFn: listQuestions });
  const categories = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const parties = useQuery({ queryKey: ["parties"], queryFn: listParties });

  const cards = [
    { href: "/admin/questions", title: "Frågor", count: questions.data?.length },
    { href: "/admin/categories", title: "Kategorier", count: categories.data?.length },
    { href: "/admin/parties", title: "Partier", count: parties.data?.length },
  ];

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold tracking-tight">Administration</h1>
      <div className="grid gap-4 sm:grid-cols-3">
        {cards.map((c) => (
          <Link key={c.href} href={c.href}>
            <Card className="transition-colors hover:border-foreground/30">
              <CardHeader>
                <CardTitle className="text-base text-muted-foreground">{c.title}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-bold tabular-nums">{c.count ?? "–"}</p>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}

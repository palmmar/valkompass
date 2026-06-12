"use client";

import { useState } from "react";
import type { AdminCategory, QuestionInput } from "@/lib/admin-types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";

const selectClass =
  "h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

export function QuestionForm({
  initial,
  categories,
  busy,
  submitLabel = "Spara",
  onSubmit,
}: {
  initial: QuestionInput;
  categories: AdminCategory[];
  busy: boolean;
  submitLabel?: string;
  onSubmit: (input: QuestionInput) => void;
}) {
  const [form, setForm] = useState<QuestionInput>(initial);
  const set = <K extends keyof QuestionInput>(key: K, value: QuestionInput[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit(form);
      }}
      className="space-y-4"
    >
      <div className="space-y-1.5">
        <Label htmlFor="text">Påstående</Label>
        <Textarea id="text" value={form.text} onChange={(e) => set("text", e.target.value)} required />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="explanation">Förklaring</Label>
        <Textarea
          id="explanation"
          value={form.explanation ?? ""}
          onChange={(e) => set("explanation", e.target.value || null)}
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="explanationSourceUrl">Källänk (Läs mer)</Label>
        <Input
          id="explanationSourceUrl"
          type="url"
          placeholder="https://…"
          value={form.explanationSourceUrl ?? ""}
          onChange={(e) => set("explanationSourceUrl", e.target.value || null)}
        />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="externalKey">Nyckel (externalKey)</Label>
          <Input
            id="externalKey"
            value={form.externalKey}
            onChange={(e) => set("externalKey", e.target.value)}
            required
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="category">Kategori</Label>
          <select
            id="category"
            className={selectClass}
            value={form.categoryId || ""}
            onChange={(e) => set("categoryId", Number(e.target.value))}
            required
          >
            <option value="" disabled>
              Välj kategori…
            </option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </div>
      </div>
      <div className="grid grid-cols-2 items-end gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="order">Ordning</Label>
          <Input
            id="order"
            type="number"
            value={form.displayOrder}
            onChange={(e) => set("displayOrder", Number(e.target.value))}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="tier">Nivå (quizläge)</Label>
          <select
            id="tier"
            className={selectClass}
            value={form.tier}
            onChange={(e) => set("tier", Number(e.target.value))}
          >
            <option value={1}>1 – Snabb (25 frågor)</option>
            <option value={2}>2 – Standard (50 frågor)</option>
            <option value={3}>3 – Fördjupning (75 frågor)</option>
          </select>
        </div>
      </div>
      <label className="flex h-9 items-center gap-2">
        <Switch checked={form.isActive} onCheckedChange={(v) => set("isActive", v)} />
        <span className="text-sm">Aktiv (visas i quizet)</span>
      </label>
      <Button type="submit" disabled={busy}>
        {busy ? "Sparar…" : submitLabel}
      </Button>
    </form>
  );
}

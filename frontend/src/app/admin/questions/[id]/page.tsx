"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  getPositions,
  getQuestion,
  listCategories,
  savePositions,
  updateQuestion,
} from "@/lib/admin-api";
import type { AdminPosition, PositionInput, QuestionInput } from "@/lib/admin-types";
import { QuestionForm } from "@/components/admin/question-form";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { ChevronLeft } from "lucide-react";

const selectClass =
  "h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50";

export default function EditQuestionPage() {
  const params = useParams<{ id: string }>();
  const id = Number(params.id);
  const queryClient = useQueryClient();

  const question = useQuery({ queryKey: ["question", id], queryFn: () => getQuestion(id) });
  const categories = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const positions = useQuery({ queryKey: ["positions", id], queryFn: () => getPositions(id) });

  const saveFields = useMutation({
    mutationFn: (input: QuestionInput) => updateQuestion(id, input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["question", id] });
      queryClient.invalidateQueries({ queryKey: ["questions"] });
      toast.success("Fråga sparad.");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Fel."),
  });

  if (question.isLoading) return <p className="text-muted-foreground">Laddar…</p>;
  if (!question.data) return <p>Frågan hittades inte.</p>;

  const q = question.data;
  const initial: QuestionInput = {
    externalKey: q.externalKey,
    text: q.text,
    explanation: q.explanation,
    categoryId: q.categoryId,
    displayOrder: q.displayOrder,
    isActive: q.isActive,
  };

  return (
    <div className="space-y-6">
      <Button render={<Link href="/admin/questions" />} nativeButton={false} variant="ghost" size="sm">
        <ChevronLeft className="size-4" /> Tillbaka
      </Button>

      <Card>
        <CardHeader>
          <CardTitle>Redigera fråga</CardTitle>
        </CardHeader>
        <CardContent>
          <QuestionForm
            initial={initial}
            categories={categories.data ?? []}
            busy={saveFields.isPending}
            onSubmit={(input) => saveFields.mutate(input)}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Partiernas positioner</CardTitle>
        </CardHeader>
        <CardContent>
          {positions.data ? (
            <PositionsEditor questionId={id} initial={positions.data.positions} />
          ) : (
            <p className="text-muted-foreground">Laddar positioner…</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function PositionsEditor({
  questionId,
  initial,
}: {
  questionId: number;
  initial: AdminPosition[];
}) {
  const queryClient = useQueryClient();
  const [rows, setRows] = useState<AdminPosition[]>(initial);

  const update = (partyId: number, patch: Partial<AdminPosition>) =>
    setRows((rs) => rs.map((r) => (r.partyId === partyId ? { ...r, ...patch } : r)));

  const save = useMutation({
    mutationFn: () => {
      const input: PositionInput[] = rows.map((r) => ({
        partyId: r.partyId,
        value: r.value,
        motivation: r.motivation,
        sourceCitation: r.sourceCitation,
        sourceUrl: r.sourceUrl,
      }));
      return savePositions(questionId, input);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["positions", questionId] });
      toast.success("Positioner sparade.");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Fel."),
  });

  return (
    <div className="space-y-5">
      {rows.map((r) => (
        <div key={r.partyId} className="space-y-2 rounded-md border p-3">
          <div className="flex items-center justify-between">
            <span className="font-medium">
              {r.partyName} ({r.partyCode})
            </span>
            <div className="w-44">
              <select
                className={selectClass}
                value={r.value ?? ""}
                onChange={(e) =>
                  update(r.partyId, { value: e.target.value ? Number(e.target.value) : null })
                }
              >
                <option value="">Oklart / ingen position</option>
                <option value="1">1 – Håller inte med</option>
                <option value="2">2 – Håller delvis inte med</option>
                <option value="3">3 – Håller delvis med</option>
                <option value="4">4 – Håller helt med</option>
              </select>
            </div>
          </div>
          <Textarea
            placeholder="Motivering"
            value={r.motivation ?? ""}
            onChange={(e) => update(r.partyId, { motivation: e.target.value || null })}
          />
          <div className="grid gap-2 sm:grid-cols-2">
            <div className="space-y-1">
              <Label className="text-xs">Källa</Label>
              <Input
                placeholder="t.ex. Valmanifest 2026"
                value={r.sourceCitation ?? ""}
                onChange={(e) => update(r.partyId, { sourceCitation: e.target.value || null })}
              />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">Käll-URL</Label>
              <Input
                placeholder="https://…"
                value={r.sourceUrl ?? ""}
                onChange={(e) => update(r.partyId, { sourceUrl: e.target.value || null })}
              />
            </div>
          </div>
        </div>
      ))}
      <Button onClick={() => save.mutate()} disabled={save.isPending}>
        {save.isPending ? "Sparar…" : "Spara positioner"}
      </Button>
    </div>
  );
}

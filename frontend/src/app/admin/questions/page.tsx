"use client";

import { useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  createQuestion,
  deleteQuestion,
  listCategories,
  listQuestions,
} from "@/lib/admin-api";
import type { QuestionInput } from "@/lib/admin-types";
import { QuestionForm } from "@/components/admin/question-form";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

const EMPTY: QuestionInput = {
  externalKey: "",
  text: "",
  explanation: null,
  categoryId: 0,
  displayOrder: 0,
  isActive: true,
};

export default function QuestionsPage() {
  const queryClient = useQueryClient();
  const questions = useQuery({ queryKey: ["questions"], queryFn: listQuestions });
  const categories = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const [creating, setCreating] = useState(false);

  const create = useMutation({
    mutationFn: createQuestion,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["questions"] });
      setCreating(false);
      toast.success("Fråga skapad.");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Fel."),
  });

  const remove = useMutation({
    mutationFn: deleteQuestion,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["questions"] });
      toast.success("Borttaget.");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Fel."),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Frågor</h1>
        <Button onClick={() => setCreating(true)}>Ny fråga</Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Påstående</TableHead>
            <TableHead>Kategori</TableHead>
            <TableHead>Status</TableHead>
            <TableHead className="text-right">Åtgärd</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {questions.data?.map((q) => (
            <TableRow key={q.id}>
              <TableCell className="max-w-sm font-medium">{q.text}</TableCell>
              <TableCell className="text-muted-foreground">{q.categoryName}</TableCell>
              <TableCell>
                {q.isActive ? (
                  <Badge variant="secondary">Aktiv</Badge>
                ) : (
                  <Badge variant="outline">Inaktiv</Badge>
                )}
              </TableCell>
              <TableCell className="space-x-2 text-right">
                <Button render={<Link href={`/admin/questions/${q.id}`} />} nativeButton={false} variant="outline" size="sm">
                  Redigera
                </Button>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => {
                    if (confirm(`Ta bort frågan?`)) remove.mutate(q.id);
                  }}
                >
                  Ta bort
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Ny fråga</DialogTitle>
          </DialogHeader>
          {creating && (
            <QuestionForm
              initial={EMPTY}
              categories={categories.data ?? []}
              busy={create.isPending}
              submitLabel="Skapa"
              onSubmit={(input) => create.mutate(input)}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

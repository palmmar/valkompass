"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  createCategory,
  deleteCategory,
  listCategories,
  updateCategory,
} from "@/lib/admin-api";
import type { AdminCategory, CategoryInput } from "@/lib/admin-types";
import { Button } from "@/components/ui/button";
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
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

const EMPTY: CategoryInput = {
  slug: "",
  name: "",
  description: null,
  icon: null,
  displayOrder: 0,
};

export default function CategoriesPage() {
  const queryClient = useQueryClient();
  const { data } = useQuery({ queryKey: ["categories"], queryFn: listCategories });
  const [editing, setEditing] = useState<AdminCategory | "new" | null>(null);

  const save = useMutation({
    mutationFn: (vars: { id?: number; input: CategoryInput }) =>
      vars.id ? updateCategory(vars.id, vars.input) : createCategory(vars.input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      setEditing(null);
      toast.success("Sparat.");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Fel."),
  });

  const remove = useMutation({
    mutationFn: deleteCategory,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      toast.success("Borttaget.");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Fel."),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Kategorier</h1>
        <Button onClick={() => setEditing("new")}>Ny kategori</Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Ordning</TableHead>
            <TableHead>Namn</TableHead>
            <TableHead>Slug</TableHead>
            <TableHead className="text-right">Åtgärd</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {data?.map((c) => (
            <TableRow key={c.id}>
              <TableCell className="tabular-nums text-muted-foreground">{c.displayOrder}</TableCell>
              <TableCell className="font-medium">{c.name}</TableCell>
              <TableCell className="text-muted-foreground">{c.slug}</TableCell>
              <TableCell className="space-x-2 text-right">
                <Button variant="outline" size="sm" onClick={() => setEditing(c)}>
                  Redigera
                </Button>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => {
                    if (confirm(`Ta bort "${c.name}"?`)) remove.mutate(c.id);
                  }}
                >
                  Ta bort
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing === "new" ? "Ny kategori" : "Redigera kategori"}</DialogTitle>
          </DialogHeader>
          {editing !== null && (
            <CategoryForm
              initial={editing === "new" ? EMPTY : editing}
              busy={save.isPending}
              onSubmit={(input) =>
                save.mutate({ id: editing === "new" ? undefined : editing.id, input })
              }
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function CategoryForm({
  initial,
  busy,
  onSubmit,
}: {
  initial: CategoryInput;
  busy: boolean;
  onSubmit: (input: CategoryInput) => void;
}) {
  const [form, setForm] = useState<CategoryInput>(initial);
  const set = <K extends keyof CategoryInput>(key: K, value: CategoryInput[K]) =>
    setForm((f) => ({ ...f, [key]: value }));

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit(form);
      }}
      className="space-y-4"
    >
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="name">Namn</Label>
          <Input id="name" value={form.name} onChange={(e) => set("name", e.target.value)} required />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="slug">Slug</Label>
          <Input id="slug" value={form.slug} onChange={(e) => set("slug", e.target.value)} required />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="description">Beskrivning</Label>
        <Textarea
          id="description"
          value={form.description ?? ""}
          onChange={(e) => set("description", e.target.value || null)}
        />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="icon">Ikon</Label>
          <Input id="icon" value={form.icon ?? ""} onChange={(e) => set("icon", e.target.value || null)} />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="order">Ordning</Label>
          <Input
            id="order"
            type="number"
            value={form.displayOrder}
            onChange={(e) => set("displayOrder", Number(e.target.value))}
          />
        </div>
      </div>
      <DialogFooter>
        <Button type="submit" disabled={busy}>
          {busy ? "Sparar…" : "Spara"}
        </Button>
      </DialogFooter>
    </form>
  );
}

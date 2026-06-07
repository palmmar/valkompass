"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { createParty, deleteParty, listParties, updateParty } from "@/lib/admin-api";
import type { AdminParty, PartyInput } from "@/lib/admin-types";
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
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";

const EMPTY: PartyInput = {
  code: "",
  name: "",
  fullName: "",
  shortDescription: null,
  color: null,
  displayOrder: 0,
  isActive: true,
};

export default function PartiesPage() {
  const queryClient = useQueryClient();
  const { data } = useQuery({ queryKey: ["parties"], queryFn: listParties });
  const [editing, setEditing] = useState<AdminParty | "new" | null>(null);

  const save = useMutation({
    mutationFn: (vars: { id?: number; input: PartyInput }) =>
      vars.id ? updateParty(vars.id, vars.input) : createParty(vars.input),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["parties"] });
      setEditing(null);
      toast.success("Sparat.");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Fel."),
  });

  const remove = useMutation({
    mutationFn: deleteParty,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["parties"] });
      toast.success("Borttaget.");
    },
    onError: (e) => toast.error(e instanceof Error ? e.message : "Fel."),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Partier</h1>
        <Button onClick={() => setEditing("new")}>Nytt parti</Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Kod</TableHead>
            <TableHead>Namn</TableHead>
            <TableHead>Aktivt</TableHead>
            <TableHead className="text-right">Åtgärd</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {data?.map((p) => (
            <TableRow key={p.id}>
              <TableCell>
                <span className="flex items-center gap-2 font-medium">
                  <span
                    className="inline-block size-3 rounded-full"
                    style={{ backgroundColor: p.color ?? "#999" }}
                  />
                  {p.code}
                </span>
              </TableCell>
              <TableCell>{p.name}</TableCell>
              <TableCell className="text-muted-foreground">{p.isActive ? "Ja" : "Nej"}</TableCell>
              <TableCell className="space-x-2 text-right">
                <Button variant="outline" size="sm" onClick={() => setEditing(p)}>
                  Redigera
                </Button>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => {
                    if (confirm(`Ta bort "${p.name}"?`)) remove.mutate(p.id);
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
            <DialogTitle>{editing === "new" ? "Nytt parti" : "Redigera parti"}</DialogTitle>
          </DialogHeader>
          {editing !== null && (
            <PartyForm
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

function PartyForm({
  initial,
  busy,
  onSubmit,
}: {
  initial: PartyInput;
  busy: boolean;
  onSubmit: (input: PartyInput) => void;
}) {
  const [form, setForm] = useState<PartyInput>(initial);
  const set = <K extends keyof PartyInput>(key: K, value: PartyInput[K]) =>
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
          <Label htmlFor="code">Kod</Label>
          <Input id="code" value={form.code} onChange={(e) => set("code", e.target.value)} required />
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
      <div className="space-y-1.5">
        <Label htmlFor="name">Namn</Label>
        <Input id="name" value={form.name} onChange={(e) => set("name", e.target.value)} required />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="fullName">Fullständigt namn</Label>
        <Input
          id="fullName"
          value={form.fullName}
          onChange={(e) => set("fullName", e.target.value)}
          required
        />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="desc">Kort beskrivning</Label>
        <Textarea
          id="desc"
          value={form.shortDescription ?? ""}
          onChange={(e) => set("shortDescription", e.target.value || null)}
        />
      </div>
      <div className="grid grid-cols-2 items-end gap-3">
        <div className="space-y-1.5">
          <Label htmlFor="color">Färg (hex)</Label>
          <Input
            id="color"
            value={form.color ?? ""}
            onChange={(e) => set("color", e.target.value || null)}
            placeholder="#E8112D"
          />
        </div>
        <label className="flex h-9 items-center gap-2">
          <Switch checked={form.isActive} onCheckedChange={(v) => set("isActive", v)} />
          <span className="text-sm">Aktivt</span>
        </label>
      </div>
      <DialogFooter>
        <Button type="submit" disabled={busy}>
          {busy ? "Sparar…" : "Spara"}
        </Button>
      </DialogFooter>
    </form>
  );
}

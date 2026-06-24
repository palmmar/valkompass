"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Switch } from "@/components/ui/switch";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Star,
  SkipForward,
  ChevronLeft,
  ThumbsUp,
  ThumbsDown,
  type LucideIcon,
} from "lucide-react";
import { useQuizStore } from "@/stores/quiz-store";
import { useHydrated } from "@/hooks/use-hydrated";
import { useSyncQuizSession } from "@/hooks/use-sync-quiz-session";
import { maybePingStart, submitQuiz, type QuizMode } from "@/lib/api";
import { SCALE_OPTIONS } from "@/lib/scale";
import type { QuestionnaireCategory, QuestionnaireQuestion, SubmitQuizRequest } from "@/lib/types";
import { cn } from "@/lib/utils";

// Ikon + ton per svarssteg (1–4). Tummen upp/ner = riktning, fylld = starkt ställningstagande.
const ANSWER_STYLE: Record<number, { Icon: LucideIcon; fill: boolean; agree: boolean }> = {
  4: { Icon: ThumbsUp, fill: true, agree: true }, // Håller helt med
  3: { Icon: ThumbsUp, fill: false, agree: true }, // Håller delvis med
  2: { Icon: ThumbsDown, fill: false, agree: false }, // Håller delvis inte med
  1: { Icon: ThumbsDown, fill: true, agree: false }, // Håller inte med
};

// Visningsordning vänster→höger: emot → håller med (medhåll till höger känns mest logiskt).
const ANSWER_ORDER = [...SCALE_OPTIONS].reverse();

interface Props {
  questions: QuestionnaireQuestion[];
  categories: QuestionnaireCategory[];
  mode: QuizMode;
}

export function QuizFlow({ questions, categories, mode }: Props) {
  const router = useRouter();
  const {
    answers,
    currentIndex,
    setAnswer,
    skip,
    toggleImportant,
    setIndex,
    reset,
  } = useQuizStore();
  const hydrated = useHydrated();
  const [submitting, setSubmitting] = useState(false);

  useSyncQuizSession(mode, "standard");

  const categoryName = useMemo(() => {
    const map = new Map(categories.map((c) => [c.slug, c.name]));
    return (slug: string) => map.get(slug) ?? "";
  }, [categories]);

  const answeredCount = useMemo(
    () => questions.filter((q) => answers[q.id]?.value != null || answers[q.id]?.isSkipped).length,
    [answers, questions],
  );

  if (!hydrated) {
    return (
      <div className="mx-auto max-w-2xl space-y-6 px-4 py-10">
        <Skeleton className="h-2 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (questions.length === 0) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-16 text-center text-muted-foreground">
        Inga frågor är publicerade ännu.
      </div>
    );
  }

  const index = Math.min(currentIndex, questions.length - 1);
  const question = questions[index];
  const current = answers[question.id] ?? { value: null, isSkipped: false, isImportant: false };
  const isLast = index === questions.length - 1;
  const progress = (answeredCount / questions.length) * 100;

  // Första svaret/överhoppningen i en färsk kompass → anonym påbörjad-signal (funnel-statistik).
  function pingIfFirst() {
    if (answeredCount === 0) maybePingStart(mode, "standard");
  }

  function choose(value: number) {
    pingIfFirst();
    setAnswer(question.id, value);
    if (!isLast) setTimeout(() => setIndex(index + 1), 180);
  }

  function onSkip() {
    pingIfFirst();
    skip(question.id);
    if (!isLast) setTimeout(() => setIndex(index + 1), 120);
  }

  async function onSubmit() {
    const payload: SubmitQuizRequest = {
      mode,
      answers: questions.map((q) => {
        const a = answers[q.id];
        const answered = a && (a.value != null || a.isSkipped);
        return {
          questionId: q.id,
          value: answered && !a!.isSkipped ? a!.value : null,
          isSkipped: !answered || a!.isSkipped,
          isImportant: a?.isImportant ?? false,
        };
      }),
    };

    setSubmitting(true);
    try {
      const { shareToken } = await submitQuiz(payload);
      reset();
      router.push(`/resultat/${shareToken}`);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Något gick fel.");
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto flex h-[calc(100dvh_-_3.5rem)] max-w-2xl flex-col gap-4 px-4 py-4 sm:h-auto sm:gap-6 sm:py-8">
      <div className="shrink-0 space-y-2">
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>Fråga {index + 1} av {questions.length}</span>
          <span>{answeredCount} besvarade</span>
        </div>
        <Progress value={progress} />
      </div>

      <Card className="min-h-0 flex-1 sm:flex-none">
        <CardContent className="flex min-h-0 flex-1 flex-col gap-5 pt-6 sm:block sm:space-y-5">
          <div className="min-h-0 flex-1 space-y-3 overflow-y-auto sm:flex-none sm:overflow-visible">
            <Badge variant="secondary">{categoryName(question.categorySlug)}</Badge>
            <h2 className="text-xl font-semibold leading-snug">{question.text}</h2>
            {(question.explanation || question.explanationSourceUrl) && (
              <p className="text-sm text-muted-foreground">
                {question.explanation}
                {question.explanationSourceUrl && (
                  <>
                    {question.explanation ? " " : ""}
                    <a
                      href={question.explanationSourceUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="whitespace-nowrap underline underline-offset-2"
                    >
                      Läs mer ›
                    </a>
                  </>
                )}
              </p>
            )}
          </div>

          {/* key per fråga → knapparna monteras om vid frågebyte, så att ett "fastnat"
              hover-/aktivt läge från en knapptryckning inte följer med till nästa fråga
              (förekommer i in-app-webbläsare, t.ex. Messenger, som rapporterar hover-stöd). */}
          <div key={question.id} className="grid shrink-0 grid-cols-4 gap-2 sm:gap-3">
            {ANSWER_ORDER.map((opt) => {
              const selected = current.value === opt.value && !current.isSkipped;
              const cfg = ANSWER_STYLE[opt.value];
              return (
                <Button
                  key={opt.value}
                  variant="outline"
                  aria-label={opt.label}
                  aria-pressed={selected}
                  className={cn(
                    "h-auto min-h-20 flex-col gap-2 whitespace-normal px-1 py-3 text-center",
                    cfg.agree ? "hover:border-primary/60" : "hover:border-destructive/60",
                    selected &&
                      (cfg.agree
                        ? "border-primary bg-primary/10 ring-2 ring-primary"
                        : "border-destructive bg-destructive/10 ring-2 ring-destructive"),
                  )}
                  onClick={(e) => {
                    e.currentTarget.blur();
                    choose(opt.value);
                  }}
                >
                  <cfg.Icon
                    className={cn("size-6", cfg.agree ? "text-primary" : "text-destructive")}
                    fill={cfg.fill ? "currentColor" : "none"}
                  />
                  <span className="text-[11px] font-medium leading-tight">{opt.short}</span>
                </Button>
              );
            })}
          </div>

          <label className="flex shrink-0 items-center gap-3 rounded-md border p-3">
            <Switch
              checked={current.isImportant}
              onCheckedChange={() => toggleImportant(question.id)}
            />
            <span className="flex items-center gap-1.5 text-sm">
              <Star className="size-4" />
              Den här frågan är extra viktig för mig (väger dubbelt)
            </span>
          </label>
        </CardContent>
      </Card>

      <div className="flex shrink-0 items-center justify-between gap-2">
        <Button
          variant="ghost"
          onClick={() => setIndex(Math.max(0, index - 1))}
          disabled={index === 0}
        >
          <ChevronLeft className="size-4" /> Föregående
        </Button>

        <div className="flex items-center gap-2">
          <Button variant="ghost" onClick={onSkip}>
            <SkipForward className="size-4" /> Hoppa över
          </Button>
          {isLast ? (
            <Button onClick={onSubmit} disabled={submitting}>
              {submitting ? "Beräknar…" : "Se resultat"}
            </Button>
          ) : (
            <Button onClick={() => setIndex(index + 1)}>Nästa</Button>
          )}
        </div>
      </div>

      {!isLast && answeredCount === questions.length && (
        <div className="shrink-0 text-center">
          <Button variant="link" onClick={onSubmit} disabled={submitting}>
            Alla frågor besvarade – se ditt resultat
          </Button>
        </div>
      )}
    </div>
  );
}

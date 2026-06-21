"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { SkipForward, ChevronLeft, ThumbsUp, ThumbsDown, FlaskConical } from "lucide-react";
import { useQuizStore } from "@/stores/quiz-store";
import { useHydrated } from "@/hooks/use-hydrated";
import { useSyncQuizSession } from "@/hooks/use-sync-quiz-session";
import { useSwipe, type SwipeDirection } from "@/hooks/use-swipe";
import { submitQuiz, type QuizMode } from "@/lib/api";
import { BINARY_OPTIONS } from "@/lib/scale";
import type { QuestionnaireCategory, QuestionnaireQuestion, SubmitQuizRequest } from "@/lib/types";
import { cn } from "@/lib/utils";

const DISAGREE = BINARY_OPTIONS[0].value; // 1, vänster
const AGREE = BINARY_OPTIONS[1].value; // 4, höger
const THRESHOLD = 90;
const EXIT_MS = 220;

interface Props {
  questions: QuestionnaireQuestion[];
  categories: QuestionnaireCategory[];
  mode: QuizMode;
}

export function SwipeFlow({ questions, categories, mode }: Props) {
  const router = useRouter();
  const { answers, currentIndex, setAnswer, skip, setIndex, reset } = useQuizStore();
  const hydrated = useHydrated();
  const [submitting, setSubmitting] = useState(false);
  const [exit, setExit] = useState<SwipeDirection | null>(null);

  useSyncQuizSession(mode, "swipe");

  const categoryName = useMemo(() => {
    const map = new Map(categories.map((c) => [c.slug, c.name]));
    return (slug: string) => map.get(slug) ?? "";
  }, [categories]);

  const answeredCount = useMemo(
    () => questions.filter((q) => answers[q.id]?.value != null || answers[q.id]?.isSkipped).length,
    [answers, questions],
  );

  const index = Math.min(currentIndex, Math.max(0, questions.length - 1));
  const isLast = index === questions.length - 1;
  const question = questions[index];

  // Skicka in och gå till resultatet. Läser färskaste svaren direkt från storen så att även
  // ett svar som precis sparats (t.ex. på sista kortet) hinner komma med.
  const submit = useCallback(async () => {
    const latest = useQuizStore.getState().answers;
    const payload: SubmitQuizRequest = {
      simplified: true,
      answers: questions.map((q) => {
        const a = latest[q.id];
        const answered = a && (a.value != null || a.isSkipped);
        return {
          questionId: q.id,
          value: answered && !a!.isSkipped ? a!.value : null,
          isSkipped: !answered || a!.isSkipped,
          isImportant: false,
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
  }, [questions, reset, router]);

  // Spara svaret och animera ut kortet. På sista frågan går vi direkt till resultatet.
  const commit = useCallback(
    (value: number) => {
      if (exit || submitting || !question) return;
      setAnswer(question.id, value);
      setExit(value === AGREE ? "right" : "left");
      setTimeout(() => {
        if (isLast) {
          submit();
        } else {
          setExit(null);
          setIndex(index + 1);
        }
      }, EXIT_MS);
    },
    [exit, submitting, question, isLast, setAnswer, setIndex, index, submit],
  );

  const { dx, dragging, bind } = useSwipe({
    threshold: THRESHOLD,
    disabled: exit !== null,
    onSwipe: (dir) => commit(dir === "right" ? AGREE : DISAGREE),
  });

  // Tangentbord: ←/→ svarar (desktop + tillgänglighet).
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === "ArrowRight") commit(AGREE);
      else if (e.key === "ArrowLeft") commit(DISAGREE);
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [commit]);

  if (!hydrated) {
    return (
      <div className="mx-auto max-w-xl space-y-6 px-4 py-10">
        <Skeleton className="h-2 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (questions.length === 0) {
    return (
      <div className="mx-auto max-w-xl px-4 py-16 text-center text-muted-foreground">
        Inga frågor är publicerade ännu.
      </div>
    );
  }

  const progress = (answeredCount / questions.length) * 100;
  const current = answers[question.id];
  const chosen = current && !current.isSkipped ? current.value : null;

  const rotate = dragging ? dx * 0.04 : 0;
  const transform = exit
    ? `translateX(${exit === "right" ? 140 : -140}%) rotate(${exit === "right" ? 12 : -12}deg)`
    : `translateX(${dx}px) rotate(${rotate}deg)`;
  const agreeHint = Math.max(0, Math.min(1, dx / THRESHOLD));
  const disagreeHint = Math.max(0, Math.min(1, -dx / THRESHOLD));

  function onSkip() {
    if (exit || !question) return;
    skip(question.id);
    if (!isLast) setIndex(index + 1);
  }

  return (
    <div className="mx-auto max-w-xl space-y-6 px-4 py-8">
      <Alert>
        <FlaskConical className="size-4" />
        <AlertTitle>Experimentellt läge</AlertTitle>
        <AlertDescription>
          Ett snabbare test där du bara väljer håller med eller inte. Det kan ge en mindre
          träffsäker bild än det vanliga testet.
        </AlertDescription>
      </Alert>

      <div className="space-y-2">
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>Fråga {index + 1} av {questions.length}</span>
          <span>{answeredCount} besvarade</span>
        </div>
        <Progress value={progress} />
      </div>

      {/* key per fråga → kortet monteras om vid frågebyte (nollställer drag/transform). */}
      <Card
        key={question.id}
        {...bind}
        style={{ transform, transition: dragging ? "none" : `transform ${EXIT_MS}ms ease-out` }}
        className="relative touch-none cursor-grab select-none active:cursor-grabbing"
      >
        <span
          className="pointer-events-none absolute left-4 top-4 z-10 -rotate-12 rounded-md border-2 border-red-600 px-2 py-1 text-sm font-bold uppercase text-red-600"
          style={{ opacity: disagreeHint }}
        >
          Håller inte med
        </span>
        <span
          className="pointer-events-none absolute right-4 top-4 z-10 rotate-12 rounded-md border-2 border-green-600 px-2 py-1 text-sm font-bold uppercase text-green-600"
          style={{ opacity: agreeHint }}
        >
          Håller med
        </span>

        <CardContent className="flex min-h-56 flex-col justify-center gap-3 py-8">
          <Badge variant="secondary" className="w-fit">{categoryName(question.categorySlug)}</Badge>
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
        </CardContent>
      </Card>

      <p className="text-center text-xs text-muted-foreground">
        Svep åt höger för att hålla med, åt vänster för att inte hålla med – eller använd knapparna.
      </p>

      <div className="grid grid-cols-2 gap-3">
        <Button
          variant="outline"
          aria-label={BINARY_OPTIONS[0].label}
          className={cn("h-14 border-red-600", chosen === DISAGREE && "ring-2 ring-ring")}
          onClick={(e) => {
            e.currentTarget.blur();
            commit(DISAGREE);
          }}
        >
          <ThumbsDown className="size-6 text-red-600" />
        </Button>
        <Button
          variant="outline"
          aria-label={BINARY_OPTIONS[1].label}
          className={cn("h-14 border-green-600", chosen === AGREE && "ring-2 ring-ring")}
          onClick={(e) => {
            e.currentTarget.blur();
            commit(AGREE);
          }}
        >
          <ThumbsUp className="size-6 text-green-600" />
        </Button>
      </div>

      <div className="flex items-center justify-between gap-2">
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
          {isLast && (
            <Button onClick={submit} disabled={submitting}>
              {submitting ? "Beräknar…" : "Se resultat"}
            </Button>
          )}
        </div>
      </div>

      {!isLast && answeredCount === questions.length && (
        <div className="text-center">
          <Button variant="link" onClick={submit} disabled={submitting}>
            Alla frågor besvarade – se ditt resultat
          </Button>
        </div>
      )}
    </div>
  );
}

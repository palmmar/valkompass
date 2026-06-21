"use client";

import { useEffect } from "react";
import { useQuizStore } from "@/stores/quiz-store";
import type { QuizMode, QuizVariant } from "@/lib/api";

/**
 * Håller lagrat läge + variant i synk med URL:en så att resume-kortet vet vilket
 * frågeset/format som ska återupptas. Byter användaren läge eller variant (t.ex. via
 * direktlänk) börjar vi om för den nya kombinationen i stället för att blanda svar.
 */
export function useSyncQuizSession(mode: QuizMode, variant: QuizVariant) {
  const storeMode = useQuizStore((s) => s.mode);
  const storeVariant = useQuizStore((s) => s.variant);
  const setSession = useQuizStore((s) => s.setSession);
  const start = useQuizStore((s) => s.start);

  useEffect(() => {
    if (storeMode == null) setSession(mode, variant);
    else if (storeMode !== mode || storeVariant !== variant) start(mode, variant);
  }, [mode, variant, storeMode, storeVariant, setSession, start]);
}

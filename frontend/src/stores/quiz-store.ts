"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { QuizMode, QuizVariant } from "@/lib/api";

export interface AnswerState {
  value: number | null;
  isSkipped: boolean;
  isImportant: boolean;
}

interface QuizState {
  /** Valt läge (antal frågor) för den påbörjade kompassen. */
  mode: QuizMode | null;
  /** Vald variant: standard knappval eller förenklat swajp-läge. */
  variant: QuizVariant;
  /** Svar per fråge-id. */
  answers: Record<number, AnswerState>;
  currentIndex: number;

  setAnswer: (questionId: number, value: number) => void;
  skip: (questionId: number) => void;
  toggleImportant: (questionId: number) => void;
  setIndex: (index: number) => void;
  /** Färsk start: nollställ svar och sätt nytt läge + variant. */
  start: (mode: QuizMode, variant?: QuizVariant) => void;
  /** Synka lagrat läge + variant med URL:en utan att röra svaren. */
  setSession: (mode: QuizMode, variant: QuizVariant) => void;
  reset: () => void;
}

function ensure(answers: Record<number, AnswerState>, id: number): AnswerState {
  return answers[id] ?? { value: null, isSkipped: false, isImportant: false };
}

export const useQuizStore = create<QuizState>()(
  persist(
    (set) => ({
      mode: null,
      variant: "standard",
      answers: {},
      currentIndex: 0,

      setAnswer: (questionId, value) =>
        set((state) => {
          const prev = ensure(state.answers, questionId);
          return {
            answers: {
              ...state.answers,
              [questionId]: { ...prev, value, isSkipped: false },
            },
          };
        }),

      skip: (questionId) =>
        set((state) => {
          const prev = ensure(state.answers, questionId);
          return {
            answers: {
              ...state.answers,
              [questionId]: { ...prev, value: null, isSkipped: true },
            },
          };
        }),

      toggleImportant: (questionId) =>
        set((state) => {
          const prev = ensure(state.answers, questionId);
          return {
            answers: {
              ...state.answers,
              [questionId]: { ...prev, isImportant: !prev.isImportant },
            },
          };
        }),

      setIndex: (index) => set({ currentIndex: index }),
      start: (mode, variant = "standard") => set({ answers: {}, currentIndex: 0, mode, variant }),
      setSession: (mode, variant) => set({ mode, variant }),
      reset: () => set({ answers: {}, currentIndex: 0, mode: null, variant: "standard" }),
    }),
    {
      name: "valkompass-quiz",
      version: 1,
      // localStorage: framsteg överlever omladdning OCH att fliken/webbläsaren
      // stängs, så att användaren kan återuppta sin kompass en annan gång.
      partialize: (state) => ({
        mode: state.mode,
        variant: state.variant,
        answers: state.answers,
        currentIndex: state.currentIndex,
      }),
      storage: {
        getItem: (name) => {
          if (typeof window === "undefined") return null;
          const value = window.localStorage.getItem(name);
          return value ? JSON.parse(value) : null;
        },
        setItem: (name, value) => {
          if (typeof window !== "undefined")
            window.localStorage.setItem(name, JSON.stringify(value));
        },
        removeItem: (name) => {
          if (typeof window !== "undefined") window.localStorage.removeItem(name);
        },
      },
    },
  ),
);

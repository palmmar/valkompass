"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";

export interface AnswerState {
  value: number | null;
  isSkipped: boolean;
  isImportant: boolean;
}

interface QuizState {
  /** Svar per fråge-id. */
  answers: Record<number, AnswerState>;
  currentIndex: number;

  setAnswer: (questionId: number, value: number) => void;
  skip: (questionId: number) => void;
  toggleImportant: (questionId: number) => void;
  setIndex: (index: number) => void;
  reset: () => void;
}

function ensure(answers: Record<number, AnswerState>, id: number): AnswerState {
  return answers[id] ?? { value: null, isSkipped: false, isImportant: false };
}

export const useQuizStore = create<QuizState>()(
  persist(
    (set) => ({
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
      reset: () => set({ answers: {}, currentIndex: 0 }),
    }),
    {
      name: "valkompass-quiz",
      // sessionStorage: framsteg överlever omladdning men inte ny flik/session.
      storage: {
        getItem: (name) => {
          if (typeof window === "undefined") return null;
          const value = window.sessionStorage.getItem(name);
          return value ? JSON.parse(value) : null;
        },
        setItem: (name, value) => {
          if (typeof window !== "undefined")
            window.sessionStorage.setItem(name, JSON.stringify(value));
        },
        removeItem: (name) => {
          if (typeof window !== "undefined") window.sessionStorage.removeItem(name);
        },
      },
    },
  ),
);

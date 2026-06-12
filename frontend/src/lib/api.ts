import type {
  Questionnaire,
  ResultDocument,
  SubmitQuizRequest,
  SubmitQuizResponse,
} from "@/lib/types";

// Server-side bas-URL (Server Components anropar backend direkt).
const SERVER_BASE = process.env.BACKEND_URL ?? "http://localhost:5208";
// Klient-bas: tom sträng → relativa /api-anrop som Next proxar (se next.config.ts).
const CLIENT_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

/** Quizlägen: antal frågor per genomgång. */
export const QUIZ_MODES = [25, 50, 75] as const;
export type QuizMode = (typeof QUIZ_MODES)[number];

/** Hämtar frågeformuläret server-side. `mode` styr antalet frågor (utelämnad = alla). */
export async function fetchQuestionnaire(mode?: QuizMode): Promise<Questionnaire> {
  const query = mode ? `?mode=${mode}` : "";
  const res = await fetch(`${SERVER_BASE}/api/questionnaire${query}`, { cache: "no-store" });
  if (!res.ok) throw new Error(`Kunde inte hämta frågeformuläret (${res.status}).`);
  return res.json();
}

/** Hämtar ett resultat server-side. Returnerar null vid 404. */
export async function fetchResult(token: string): Promise<ResultDocument | null> {
  const res = await fetch(`${SERVER_BASE}/api/results/${encodeURIComponent(token)}`, {
    cache: "no-store",
  });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`Kunde inte hämta resultatet (${res.status}).`);
  return res.json();
}

/** Skickar in svar (klient-side, proxas till backend). */
export async function submitQuiz(
  request: SubmitQuizRequest,
): Promise<SubmitQuizResponse> {
  const res = await fetch(`${CLIENT_BASE}/api/quiz/results`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!res.ok) {
    let message = `Något gick fel (${res.status}).`;
    try {
      const problem = await res.json();
      const errors = problem?.errors?.answers as string[] | undefined;
      if (errors?.length) message = errors.join(" ");
    } catch {
      // ignorera parsningsfel
    }
    throw new Error(message);
  }
  return res.json();
}

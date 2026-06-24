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

/** Quizvariant: standard knappval, eller förenklat binärt swajp-läge (experimentellt). */
export const QUIZ_VARIANTS = ["standard", "swipe"] as const;
export type QuizVariant = (typeof QUIZ_VARIANTS)[number];

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

/**
 * Anonym, best-effort signal om att en kompass påbörjats (för funnel-statistik). Skickas en gång
 * per webbläsarsession och läge+variant (sessionStorage-flagga – ingen cookie, ingen PII). Fel
 * sväljs medvetet: telemetri får aldrig störa själva testet.
 */
export function maybePingStart(mode: QuizMode, variant: QuizVariant): void {
  if (typeof window === "undefined") return;
  const key = `valkompass-started:${mode}:${variant}`;
  try {
    if (window.sessionStorage.getItem(key)) return;
    window.sessionStorage.setItem(key, "1");
  } catch {
    // sessionStorage kan vara blockerad – hoppa då bara över dedupningen, inte pingen.
  }
  void fetch(`${CLIENT_BASE}/api/quiz/start`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ mode, simplified: variant === "swipe" }),
    keepalive: true,
  }).catch(() => {
    // Tappad ping underskattar bara antalet påbörjade – inget att visa användaren.
  });
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

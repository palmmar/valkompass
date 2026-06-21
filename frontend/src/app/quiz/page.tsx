import { fetchQuestionnaire, QUIZ_MODES, type QuizMode } from "@/lib/api";
import { QuizFlow } from "@/components/quiz/quiz-flow";
import { SwipeFlow } from "@/components/quiz/swipe-flow";
import { ModeSelect } from "@/components/quiz/mode-select";

export const dynamic = "force-dynamic";

export default async function QuizPage({
  searchParams,
}: {
  searchParams: Promise<{ mode?: string | string[]; format?: string | string[] }>;
}) {
  const params = await searchParams;
  const raw = params.mode;
  const mode = Number(Array.isArray(raw) ? raw[0] : raw);
  const format = Array.isArray(params.format) ? params.format[0] : params.format;

  if (!QUIZ_MODES.includes(mode as QuizMode)) return <ModeSelect />;

  const data = await fetchQuestionnaire(mode as QuizMode);

  // Det förenklade swajp-testet gäller bara snabbtestet (25 frågor).
  if (mode === 25 && format === "swipe") {
    return <SwipeFlow questions={data.questions} categories={data.categories} mode={mode as QuizMode} />;
  }

  return <QuizFlow questions={data.questions} categories={data.categories} mode={mode as QuizMode} />;
}

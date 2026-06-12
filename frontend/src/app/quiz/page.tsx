import { fetchQuestionnaire, QUIZ_MODES, type QuizMode } from "@/lib/api";
import { QuizFlow } from "@/components/quiz/quiz-flow";
import { ModeSelect } from "@/components/quiz/mode-select";

export const dynamic = "force-dynamic";

export default async function QuizPage({
  searchParams,
}: {
  searchParams: Promise<{ mode?: string | string[] }>;
}) {
  const raw = (await searchParams).mode;
  const mode = Number(Array.isArray(raw) ? raw[0] : raw);

  if (!QUIZ_MODES.includes(mode as QuizMode)) return <ModeSelect />;

  const data = await fetchQuestionnaire(mode as QuizMode);
  return <QuizFlow questions={data.questions} categories={data.categories} />;
}

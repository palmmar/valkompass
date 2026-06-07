import { fetchQuestionnaire } from "@/lib/api";
import { QuizFlow } from "@/components/quiz/quiz-flow";

export const dynamic = "force-dynamic";

export default async function QuizPage() {
  const data = await fetchQuestionnaire();
  return <QuizFlow questions={data.questions} categories={data.categories} />;
}

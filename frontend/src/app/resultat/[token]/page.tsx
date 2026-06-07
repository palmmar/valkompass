import { notFound } from "next/navigation";
import { fetchResult } from "@/lib/api";
import { ResultsView } from "@/components/results/results-view";

export const dynamic = "force-dynamic";

export default async function ResultPage({
  params,
}: {
  params: Promise<{ token: string }>;
}) {
  const { token } = await params;
  const doc = await fetchResult(token);
  if (!doc) notFound();
  return <ResultsView doc={doc} />;
}

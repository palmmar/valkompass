// Körs en gång när Next-servern startar, innan den tar emot någon förfrågan.
// Se src/lib/metrics/server.ts för vad som mäts och varför.
export async function register(): Promise<void> {
  // Edge-runtime saknar node:http och prom-client. Dynamisk import så att koden aldrig
  // följer med i edge-bundlen.
  if (process.env.NEXT_RUNTIME !== "nodejs") return;

  const { startMetrics } = await import("./lib/metrics/server");
  startMetrics();
}

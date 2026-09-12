// Liveness-endpoint för containerns och poddens hälsokontroller.
//
// Medvetet statisk och utan anrop till backend: probena ska svara på om Next-servern
// lever, inte på om API:t gör det. Annars skulle en nere-backend få Kubernetes att döda
// även frontend-podden, trots att den mycket väl kan rendera sidor.
export const dynamic = "force-static";

export function GET() {
  return Response.json({ status: "ok" });
}

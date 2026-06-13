"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";

// Egen, fristående TanStack Query-provider för barometern (publik read-only-data). Delar
// medvetet ingen klientstate med quizflödet. Opinionsdata uppdateras sällan → lång staleTime.
export function BarometerQueryProvider({ children }: { children: React.ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { retry: false, refetchOnWindowFocus: false, staleTime: 5 * 60_000 },
        },
      }),
  );
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

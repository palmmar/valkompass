"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";

/**
 * Egen TanStack Query-provider för valvakan. Till skillnad från barometern (som uppdateras
 * någon gång per dygn) ändras den här datan var halvminut under valnatten, så staleTime är
 * kort och pollningen sköts av queryn själv – ingen full sidladdning.
 *
 * Att polla tätt kostar ingenting uppströms: backend hämtar från Valmyndigheten på sitt eget
 * intervall oavsett hur många som tittar, och API-svaret cachas några sekunder.
 */
export function ElectionQueryProvider({ children }: { children: React.ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            // Ett tillfälligt fel ska inte tömma sidan – behåll förra svaret och försök igen.
            retry: 2,
            refetchOnWindowFocus: true,
            staleTime: 10_000,
          },
        },
      }),
  );
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

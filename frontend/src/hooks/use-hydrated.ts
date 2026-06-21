"use client";

import { useSyncExternalStore } from "react";

const subscribe = () => () => {};

/**
 * Returnerar false under SSR och vid första klientrenderingen, sedan true.
 * Används för att vänta in hydrering innan klient-only state (t.ex. värden
 * från localStorage via Zustand persist) läses, så att server- och
 * klientmarkup matchar och vi slipper hydration-mismatch.
 */
export function useHydrated(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false,
  );
}

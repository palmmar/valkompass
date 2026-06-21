"use client";

import { useCallback, useRef, useState, type PointerEvent } from "react";

export type SwipeDirection = "left" | "right";

interface UseSwipeOptions {
  /** Minsta horisontella förflyttning (px) för att räknas som ett svep. */
  threshold?: number;
  /** Anropas när användaren svept förbi tröskeln och släpper. */
  onSwipe: (direction: SwipeDirection) => void;
  /** Stäng av input medan ett kort animeras ut. */
  disabled?: boolean;
}

/**
 * Spårar horisontell dragning med native Pointer Events (mus + touch). Returnerar aktuell
 * förflyttning (`dx`) och `dragging`-flagga så att kortet kan renderas med transform/transition,
 * samt `bind` att sprida på det draggbara elementet. Vid släpp förbi tröskeln anropas `onSwipe`.
 */
export function useSwipe({ threshold = 90, onSwipe, disabled = false }: UseSwipeOptions) {
  const [dx, setDx] = useState(0);
  const [dragging, setDragging] = useState(false);
  const startX = useRef(0);
  const active = useRef(false);

  const onPointerDown = useCallback(
    (e: PointerEvent) => {
      if (disabled || (e.button != null && e.button !== 0)) return;
      active.current = true;
      startX.current = e.clientX;
      setDragging(true);
      e.currentTarget.setPointerCapture?.(e.pointerId);
    },
    [disabled],
  );

  const onPointerMove = useCallback((e: PointerEvent) => {
    if (!active.current) return;
    setDx(e.clientX - startX.current);
  }, []);

  const end = useCallback(
    (e: PointerEvent) => {
      if (!active.current) return;
      active.current = false;
      setDragging(false);
      const delta = e.clientX - startX.current;
      setDx(0);
      if (Math.abs(delta) >= threshold) onSwipe(delta > 0 ? "right" : "left");
    },
    [threshold, onSwipe],
  );

  return {
    dx,
    dragging,
    bind: {
      onPointerDown,
      onPointerMove,
      onPointerUp: end,
      onPointerCancel: end,
    },
  };
}

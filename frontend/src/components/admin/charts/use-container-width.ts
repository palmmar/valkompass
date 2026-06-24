"use client";

import { useEffect, useRef, useState } from "react";

/** Mäter containerns bredd via ResizeObserver så att diagrammen blir responsiva. */
export function useContainerWidth(defaultWidth = 640) {
  const ref = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(defaultWidth);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const update = () => setWidth(el.clientWidth || defaultWidth);
    update();
    const ro = new ResizeObserver(update);
    ro.observe(el);
    return () => ro.disconnect();
  }, [defaultWidth]);
  return [ref, width] as const;
}

/** Datafärger för admin-diagrammen (läsbara i både ljust och mörkt läge). */
export const STARTED_COLOR = "#94a3b8"; // slate-400 – toppen av funneln (påbörjade)
export const COMPLETED_COLOR = "#059669"; // emerald-600 – slutförda

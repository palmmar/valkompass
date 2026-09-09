// App-övergripande konstanter.

// Valdagen 2026: andra söndagen i september (vallagen). September ligger i CEST (+02:00).
// Nedräkningen siktar på när vallokalerna stänger kl. 20:00 – inte på midnatt, då den annars
// hade nollställts ett dygn för tidigt och sagt "Valet är genomfört" under hela valdagen.
export const POLLS_CLOSE = new Date("2026-09-13T20:00:00+02:00");

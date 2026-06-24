import type { Metadata } from "next";
import { Fraunces, Libre_Franklin, Spline_Sans_Mono } from "next/font/google";
import "./globals.css";
import { Toaster } from "@/components/ui/sonner";
import { SiteHeader } from "@/components/site-header";
import { SiteFooter } from "@/components/site-footer";

// Fraunces – redaktionell display-serif för rubriker. Libre Franklin – nyhetsmässig
// brödtext. Spline Sans Mono – etiketter, siffror och datatabeller (datajournalistik-ton).
const fraunces = Fraunces({
  variable: "--font-heading",
  subsets: ["latin"],
  axes: ["opsz"],
  display: "swap",
});

const libreFranklin = Libre_Franklin({
  variable: "--font-sans",
  subsets: ["latin"],
  display: "swap",
});

const splineMono = Spline_Sans_Mono({
  variable: "--font-mono",
  subsets: ["latin"],
  display: "swap",
});

export const metadata: Metadata = {
  title: "Valkompass 2026",
  description:
    "Svara på frågor och se hur väl dina åsikter stämmer överens med riksdagspartierna inför valet 2026.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="sv"
      className={`${fraunces.variable} ${libreFranklin.variable} ${splineMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col bg-background text-foreground">
        <SiteHeader />
        <main className="flex-1">{children}</main>
        <SiteFooter />
        <Toaster richColors position="top-center" />
      </body>
    </html>
  );
}

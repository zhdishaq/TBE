// Phase 5 Plan 05-00 — B2B Agent Portal root layout.
//
// Source: fork of src/portals/b2c-web/app/layout.tsx + 05-PATTERNS.md §19 +
// 05-00-PLAN action step 13.
//
// Deltas vs b2c-web:
//   - `storageKey="tbe-b2b-theme"` — separate theme preference from the
//     b2c portal when both are served from the same apex domain.
//   - `<AgentPortalBadge />` rendered in the placeholder header so the
//     D-42 differentiation is visible on every page in Wave 0. The full
//     header component (with nav + wallet chip) ships in Plan 01.
//
// Wave 0 deliberately does NOT fetch the wallet balance here — wallet chip
// is Plan 05-01 per 05-UI-SPEC §Reuse vs New Ledger.

import { Suspense, type ReactNode } from 'react';
import { Inter } from 'next/font/google';
import { ThemeProvider } from 'next-themes';
import { cn } from '@/lib/utils';
import { Toaster } from '@/components/ui/sonner';
import { TooltipProvider } from '@/components/ui/tooltip';
import { AgentPortalBadge } from '@/components/layout/agent-portal-badge';
import '@/styles/globals.css';

const inter = Inter({ subsets: ['latin'] });

export const metadata = {
  title: {
    template: '%s | TBE Agent Portal',
    default: 'TBE — Agent Portal',
  },
};

interface RootLayoutProps {
  children: ReactNode;
}

export default async function RootLayout({ children }: RootLayoutProps) {
  return (
    <html className="h-full" suppressHydrationWarning>
      <body
        className={cn(
          'antialiased flex h-full flex-col text-base text-foreground bg-background',
          inter.className,
        )}
      >
        <ThemeProvider
          attribute="class"
          defaultTheme="system"
          storageKey="tbe-b2b-theme"
          enableSystem
          disableTransitionOnChange
          enableColorScheme
        >
          <TooltipProvider delayDuration={0}>
            {/* Wave 0 placeholder header — full header (nav + wallet chip)
                ships in Plan 05-01. The AgentPortalBadge MUST stay visible
                on every authenticated route per D-42. */}
            <header className="flex h-14 items-center justify-between border-b border-border px-6">
              <span className="text-sm font-semibold">TBE</span>
              <AgentPortalBadge />
            </header>
            <Suspense>{children}</Suspense>
            <Toaster />
          </TooltipProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}

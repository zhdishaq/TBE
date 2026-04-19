// Phase 5 Plan 05-00 — B2B Agent Portal root layout.
// Phase 5 Plan 05-01 deltas:
//   - The Wave 0 placeholder header (brand + badge only) was REMOVED.
//     The full authenticated Header (brand + badge + role-aware nav +
//     wallet chip + user menu) now lives in `app/(portal)/layout.tsx`
//     so unauthenticated routes (/login, the NextAuth error page)
//     render chrome-free per UI-SPEC §1 (single centred 384px card).
//   - `QueryProvider` wraps the app so admin-surface mutations
//     (`useMutation`, `useQueryClient`) work inside the Create /
//     Deactivate sub-agent dialogs.
//
// Source: fork of src/portals/b2c-web/app/layout.tsx + 05-PATTERNS.md §19.

import { Suspense, type ReactNode } from 'react';
import { Inter } from 'next/font/google';
import { ThemeProvider } from 'next-themes';
import { cn } from '@/lib/utils';
import { Toaster } from '@/components/ui/sonner';
import { TooltipProvider } from '@/components/ui/tooltip';
import { QueryProvider } from '@/components/providers/query-provider';
import '@/styles/globals.css';

const inter = Inter({ subsets: ['latin'] });

export const metadata = {
  title: {
    template: '%s | TBE Backoffice',
    default: 'TBE — Backoffice Portal',
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
          storageKey="tbe-backoffice-theme"
          enableSystem
          disableTransitionOnChange
          enableColorScheme
        >
          <QueryProvider>
            <TooltipProvider delayDuration={0}>
              <Suspense>{children}</Suspense>
              <Toaster />
            </TooltipProvider>
          </QueryProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}

'use client';

// Client-side provider wrapper: mounts the TanStack QueryClient AND the
// nuqs adapter. Kept in its own file so the results page RSC can stay a
// Server Component.

import { useState, type ReactNode } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { NuqsAdapter } from 'nuqs/adapters/next/app';
import { createQueryClient } from '@/lib/query-client';

export function ResultsProviders({ children }: { children: ReactNode }) {
  const [client] = useState(() => createQueryClient());
  return (
    <QueryClientProvider client={client}>
      <NuqsAdapter>{children}</NuqsAdapter>
    </QueryClientProvider>
  );
}

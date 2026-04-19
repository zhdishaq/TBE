// Plan 05-01 Task 2 — Client-side TanStack Query provider.
//
// The admin surface uses useMutation + useQueryClient for the sub-agent
// create / deactivate flows. QueryClient must be created per-request on
// the server and once per browser on the client; we pin it in a module
// scope and wrap the app root with the provider so Next.js hydration
// doesn't create a second client on the first client render.
//
// Defaults:
//   - `retry: 1` — one retry on failure to ride out transient
//     504s from Keycloak, but no aggressive exponential backoff loop.
//   - `refetchOnWindowFocus: false` — the sub-agent list is an
//     admin-only surface where drift rarely matters; revalidation on
//     invalidate is enough.
//
// This component is a `'use client'` boundary; the server layout
// imports it but Next renders it as a client island.

'use client';

import * as React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

function makeClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: 1,
        refetchOnWindowFocus: false,
        staleTime: 30_000,
      },
    },
  });
}

let browserClient: QueryClient | null = null;
function getClient(): QueryClient {
  if (typeof window === 'undefined') return makeClient();
  if (!browserClient) browserClient = makeClient();
  return browserClient;
}

export function QueryProvider({ children }: { children: React.ReactNode }) {
  const client = getClient();
  return (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

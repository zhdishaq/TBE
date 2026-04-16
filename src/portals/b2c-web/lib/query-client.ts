// TanStack Query client factory — shared across all client-side search
// components.
//
// Defaults:
//   - staleTime = 90s (Pitfall 11 "selection-phase TTL"). Going past 90s
//     risks showing a price we can't honour at book-time because the GDS
//     fare snapshot TTL is 90s. Do NOT raise this on any results surface.
//   - refetchOnWindowFocus = false. A refocus-refetch on the results page
//     would mutate prices while the user is considering an option.
//   - retry = 1 with 250ms backoff — one quick retry for transient 5xx
//     from the gateway; further failures surface to the user.

import { QueryClient } from '@tanstack/react-query';

export const SEARCH_STALE_TIME = 90_000;

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: SEARCH_STALE_TIME,
        refetchOnWindowFocus: false,
        retry: 1,
        retryDelay: 250,
      },
      mutations: {
        retry: 0,
      },
    },
  });
}

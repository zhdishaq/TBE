// Plan 06-01 Task 3 — backoffice portal differentiation badge.
//
// Source: 06-UI-SPEC §Portal Differentiation + 06-CONTEXT.md D-46.
// Analog: mirrors src/portals/b2b-web/components/layout/agent-portal-badge.tsx
// (per PATTERNS.md Pattern M), swapping indigo-600 → slate-900 /
// indigo-400 → slate-200 per the backoffice palette.
//
// Rendered on every authenticated route of the backoffice portal. Outline-
// only. Announced via `aria-label="Backoffice portal"` because "BACKOFFICE"
// in uppercase typography is not readable as plain English to every screen
// reader.
//
// Design tokens:
//   - border: slate-900 (light) / slate-200 (dark)
//   - text:   slate-900 (light) / slate-200 (dark)
//   - height: h-8 (32px) per 06-UI-SPEC §Spacing scale
//   - typography: uppercase tracking-wide text-xs font-semibold

import { cn } from '@/lib/utils';

export function BackofficePortalBadge({ className }: { className?: string }) {
  return (
    <span
      aria-label="Backoffice portal"
      className={cn(
        'inline-flex h-8 items-center rounded-full border border-slate-900 px-3',
        'text-xs font-semibold uppercase tracking-wide text-slate-900',
        'dark:border-slate-200 dark:text-slate-200',
        className,
      )}
    >
      BACKOFFICE
    </span>
  );
}

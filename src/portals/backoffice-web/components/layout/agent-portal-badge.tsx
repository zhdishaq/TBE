// Phase 5 Plan 05-00 — B2B Agent Portal differentiation badge.
//
// Source: 05-UI-SPEC §Portal Differentiation + 05-CONTEXT.md D-42 / D-44.
// Analog: none (NEW per 05-PATTERNS §20 "no-analog ledger").
//
// Rendered on every authenticated route of the B2B portal. Outline-only —
// NEVER fill the pill. Announced via `aria-label="Agent portal"` because
// the word "agent" is not obvious from the visual alone to assistive tech.
//
// Design tokens (D-42):
//   - border: indigo-600 (light) / indigo-400 (dark)
//   - text:   indigo-600 (light) / indigo-400 (dark)
//   - height: h-8 (32px) per 05-UI-SPEC §Spacing
//   - typography: uppercase tracking-wide text-xs font-semibold — reserved
//     for this badge per 05-UI-SPEC §Color reserved-for list item 5.

import { cn } from '@/lib/utils';

export function AgentPortalBadge({ className }: { className?: string }) {
  return (
    <span
      aria-label="Agent portal"
      className={cn(
        'inline-flex h-8 items-center rounded-full border border-indigo-600 px-3',
        'text-xs font-semibold uppercase tracking-wide text-indigo-600',
        'dark:border-indigo-400 dark:text-indigo-400',
        className,
      )}
    >
      Agent portal
    </span>
  );
}

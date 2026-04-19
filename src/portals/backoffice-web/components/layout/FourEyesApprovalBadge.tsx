// Plan 06-01 Task 3 — 4-eyes approval badge.
//
// Source: 06-UI-SPEC §Approvals surface + D-48 (4-eyes state machine).
// Analog: none (NEW — no 4-eyes flow existed before Phase 6).
//
// Rendered in the backoffice header when the signed-in viewer is eligible
// to approve pending requests (i.e. ops-admin with non-zero PendingApproval
// counts they did not themselves open). The badge communicates "action
// required" *informationally* — we deliberately use role="status" and NOT
// role="alert" because the UX spec forbids auto-announcing on every
// navigation; the live region is polite, not assertive.
//
// Design tokens (06-UI-SPEC §Approvals colour ramp):
//   - background: amber-50 (light), amber-950 alpha-30 (dark)
//   - text:       amber-900 (light), amber-100 (dark)
//   - border:     amber-600 (light), amber-500 (dark)
//   - count pill: bg-amber-600 / text-white
//
// Pitfall (a11y): role="status" NOT role="alert" — per 06-UI-SPEC §A11y
// ("polite region, not assertive") and to avoid screen-reader fatigue when
// the count updates on the 60s poll.

import { cn } from '@/lib/utils';

interface FourEyesApprovalBadgeProps {
  pendingCount: number;
  className?: string;
}

export function FourEyesApprovalBadge({
  pendingCount,
  className,
}: FourEyesApprovalBadgeProps) {
  if (pendingCount <= 0) return null;
  const label =
    pendingCount === 1
      ? '1 approval pending'
      : `${pendingCount} approvals pending`;
  return (
    <span
      role="status"
      aria-live="polite"
      aria-label={label}
      className={cn(
        'inline-flex h-8 items-center gap-2 rounded-full border px-3',
        'border-amber-600 bg-amber-50 text-amber-900',
        'dark:border-amber-500 dark:bg-amber-950/30 dark:text-amber-100',
        'text-xs font-semibold uppercase tracking-wide',
        className,
      )}
    >
      <span className="inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-amber-600 px-1.5 text-[11px] text-white tabular-nums">
        {pendingCount}
      </span>
      4-eyes queue
    </span>
  );
}

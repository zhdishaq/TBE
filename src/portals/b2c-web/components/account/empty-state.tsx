// Shared empty-state card used by dashboard, results and trip-builder
// zero-state surfaces.
//
// Copy must always be the exact strings from
// `.planning/phases/04-b2c-portal-customer-facing/04-UI-SPEC.md`
// §Copywriting Contract — callers pass them in as props; this file
// intentionally never hardcodes surface-specific text.

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

export interface EmptyStateAction {
  href: string;
  label: string;
}

export interface EmptyStateProps {
  heading: string;
  body: string;
  action?: EmptyStateAction;
  className?: string;
}

export function EmptyState({
  heading,
  body,
  action,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        // UI-SPEC §Spacing Scale — card padding md (16px) with extra
        // vertical rhythm to distinguish zero-state from a loaded list.
        'flex flex-col items-center justify-center text-center',
        'gap-4 rounded-lg border border-border bg-card p-8',
        className,
      )}
      role="status"
    >
      {/* UI-SPEC §Typography — Heading role = 20px / 600 / 1.3 */}
      <h2 className="text-xl font-semibold leading-tight">{heading}</h2>
      {/* Body = 16px / 400 / 1.5 */}
      <p className="max-w-md text-base text-muted-foreground">{body}</p>
      {action ? (
        <Button asChild>
          <Link href={action.href}>{action.label}</Link>
        </Button>
      ) : null}
    </div>
  );
}

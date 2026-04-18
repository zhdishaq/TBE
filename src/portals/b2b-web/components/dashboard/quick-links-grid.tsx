// Plan 05-04 Task 3 — QuickLinksGrid.
//
// 2x2 tile grid (D-44) giving one-click access to the most-used surfaces.
// The wallet top-up tile is admin-only — rendered only when
// `roles.includes('agent-admin')` (also enforced server-side on
// /admin/wallet per Plan 05-03).

import Link from 'next/link';

interface QuickLinksGridProps {
  roles: string[];
}

interface Tile {
  href: string;
  label: string;
  description: string;
  adminOnly?: boolean;
}

const tiles: Tile[] = [
  {
    href: '/search/flights',
    label: 'New flight search',
    description: 'Find itineraries and price quotes for your clients.',
  },
  {
    href: '/bookings',
    label: 'My bookings',
    description: 'Filter, void, and download invoices / e-tickets.',
  },
  {
    href: '/admin/agents',
    label: 'Manage agents',
    description: 'Invite, deactivate, and manage sub-agents.',
    adminOnly: true,
  },
  {
    href: '/admin/wallet',
    label: 'Wallet top-up',
    description: 'Add funds, view ledger, and set alert thresholds.',
    adminOnly: true,
  },
];

export function QuickLinksGrid({ roles }: QuickLinksGridProps) {
  const isAdmin = roles.includes('agent-admin');
  const visible = tiles.filter((t) => !t.adminOnly || isAdmin);
  return (
    <section
      aria-labelledby="quick-links-heading"
      className="rounded-lg border border-border bg-card p-6 shadow-sm"
    >
      <h2
        id="quick-links-heading"
        className="mb-4 text-lg font-semibold text-foreground"
      >
        Quick links
      </h2>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {visible.map((t) => (
          <Link
            key={t.href}
            href={t.href}
            className="rounded-md border border-border bg-background p-4 transition-colors hover:bg-accent focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          >
            <p className="text-sm font-medium text-foreground">{t.label}</p>
            <p className="mt-1 text-xs text-muted-foreground">{t.description}</p>
          </Link>
        ))}
      </div>
    </section>
  );
}

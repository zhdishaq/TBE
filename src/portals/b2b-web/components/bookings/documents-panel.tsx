// Plan 05-04 Task 3 — DocumentsPanel.
//
// Side-panel on the booking detail that lists downloadable documents:
//   - Invoice PDF   (always available once AgencyGrossAmount is set)
//   - E-ticket PDF  (only when TicketNumber is present)
//
// Links go through the route-handler proxies (/api/bookings/[id]/invoice.pdf
// and /api/bookings/[id]/e-ticket.pdf) which stream the upstream body via
// Pitfall 11 `new Response(upstream.body, ...)`.

import Link from 'next/link';

interface DocumentsPanelProps {
  bookingId: string;
  ticketNumber?: string | null;
  hasInvoice: boolean;
}

export function DocumentsPanel({
  bookingId,
  ticketNumber,
  hasInvoice,
}: DocumentsPanelProps) {
  const docs: { href: string; label: string; description: string }[] = [];
  if (hasInvoice) {
    docs.push({
      href: `/api/bookings/${bookingId}/invoice.pdf`,
      label: 'Invoice (PDF)',
      description: 'Customer-facing invoice — gross only.',
    });
  }
  if (ticketNumber) {
    docs.push({
      href: `/api/bookings/${bookingId}/e-ticket.pdf`,
      label: 'E-ticket (PDF)',
      description: `Ticket ${ticketNumber}`,
    });
  }
  return (
    <section
      aria-labelledby="documents-heading"
      className="rounded-lg border border-border bg-card p-6 shadow-sm"
    >
      <h2
        id="documents-heading"
        className="mb-4 text-lg font-semibold text-foreground"
      >
        Documents
      </h2>
      {docs.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Documents become available once the booking is ticketed.
        </p>
      ) : (
        <ul className="flex flex-col gap-2">
          {docs.map((d) => (
            <li key={d.href}>
              <Link
                href={d.href}
                target="_blank"
                rel="noopener noreferrer"
                className="flex flex-col gap-0.5 rounded-md border border-border bg-background p-3 transition-colors hover:bg-accent focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              >
                <span className="text-sm font-medium text-foreground">
                  {d.label}
                </span>
                <span className="text-xs text-muted-foreground">
                  {d.description}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

// Plan 05-04 Task 3 — BookingStatusCard.
//
// Renders the headline booking detail: reference, PNR, status badge,
// TTL countdown, client, creator. Combines with TtlCountdown on the
// detail page (server-injects the deadline; the card renders the tick).

import { TtlCountdown } from './ttl-countdown';

export interface BookingStatusCardProps {
  reference: string;
  pnr: string;
  status: string;
  clientName?: string;
  agentName?: string;
  ticketingDeadlineUtc?: string;
  ticketNumber?: string;
}

export function BookingStatusCard(props: BookingStatusCardProps) {
  const {
    reference,
    pnr,
    status,
    clientName,
    agentName,
    ticketingDeadlineUtc,
    ticketNumber,
  } = props;
  return (
    <section
      aria-labelledby="booking-status-heading"
      className="rounded-lg border border-border bg-card p-6 shadow-sm"
    >
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2
            id="booking-status-heading"
            className="font-mono text-lg font-semibold text-foreground"
          >
            {reference}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            PNR <span className="font-mono">{pnr || '—'}</span>
          </p>
        </div>
        <span className="inline-flex h-7 items-center rounded-full border border-border bg-muted px-3 text-xs font-medium text-foreground">
          {status}
        </span>
      </div>

      <dl className="mt-4 grid grid-cols-1 gap-y-2 text-sm sm:grid-cols-2">
        {clientName && (
          <>
            <dt className="text-muted-foreground">Client</dt>
            <dd className="text-foreground">{clientName}</dd>
          </>
        )}
        {agentName && (
          <>
            <dt className="text-muted-foreground">Booked by</dt>
            <dd className="text-foreground">{agentName}</dd>
          </>
        )}
        {ticketNumber && (
          <>
            <dt className="text-muted-foreground">Ticket number</dt>
            <dd className="font-mono text-foreground">{ticketNumber}</dd>
          </>
        )}
        {ticketingDeadlineUtc && !ticketNumber && (
          <>
            <dt className="text-muted-foreground">Ticketing deadline</dt>
            <dd className="text-foreground">
              <TtlCountdown deadlineUtc={ticketingDeadlineUtc} />
            </dd>
          </>
        )}
      </dl>
    </section>
  );
}

// Dashboard booking row — one entry in the Upcoming / Past lists.
//
// UI-SPEC §Dashboard: "product icon (plane / bed / car) + route/property
// + dates + status badge + reference + chevron". Tapping opens the
// detail page (`/bookings/{id}`).
//
// Status color map comes from UI-SPEC §Color:
//   confirmed/ticketed → green-600 dot
//   pending/awaiting  → zinc-500 dot
//   TTL warning       → amber-600 dot
//   cancelled/failed  → red-600 dot

import Link from 'next/link';
import { Bed, Car, ChevronRight, Plane } from 'lucide-react';
import type { BookingDtoPublic } from '@/types/api';
import { formatDate, formatMoney } from '@/lib/formatters';
import { cn } from '@/lib/utils';

export interface BookingRowProps {
  booking: BookingDtoPublic;
}

/**
 * Mirrors the server-side `BookingSagaState.CurrentState` enum values.
 * 3 = ticketed/confirmed (terminal success), 4 = cancelled,
 * 5 = failed, anything lower = in-progress.
 */
function statusMeta(status: number): {
  label: string;
  dotClass: string;
} {
  switch (status) {
    case 3:
      return { label: 'Confirmed', dotClass: 'bg-green-600' };
    case 4:
      return { label: 'Cancelled', dotClass: 'bg-red-600' };
    case 5:
      return { label: 'Failed', dotClass: 'bg-red-600' };
    default:
      return { label: 'Pending', dotClass: 'bg-zinc-500' };
  }
}

function ProductIcon({
  productType,
  bookingReference,
}: {
  productType?: string;
  bookingReference: string;
}) {
  // Fall back to the booking-reference prefix until the backend starts
  // carrying productType in the list DTO (04-02+).
  const kind =
    productType ??
    (bookingReference.includes('-HTL')
      ? 'hotel'
      : bookingReference.includes('-CAR')
        ? 'car'
        : 'flight');
  if (kind === 'hotel') return <Bed className="size-5" aria-hidden />;
  if (kind === 'car') return <Car className="size-5" aria-hidden />;
  return <Plane className="size-5" aria-hidden />;
}

export function BookingRow({ booking }: BookingRowProps) {
  const status = statusMeta(booking.status);
  const date = booking.departureDate ?? booking.createdAt;
  return (
    <Link
      href={`/bookings/${booking.bookingId}`}
      className={cn(
        'flex items-center gap-4 rounded-lg border border-border bg-card p-4',
        'transition-colors hover:bg-accent/40 focus-visible:outline-none',
        'focus-visible:ring-2 focus-visible:ring-ring',
      )}
    >
      <ProductIcon
        productType={booking.productType}
        bookingReference={booking.bookingReference}
      />
      <div className="flex-1">
        <div className="font-semibold">{booking.bookingReference}</div>
        <div className="text-sm text-muted-foreground tabular-nums">
          {formatDate(date)} · {formatMoney(booking.totalAmount, booking.currency)}
        </div>
      </div>
      <div className="flex items-center gap-2 text-sm">
        <span
          className={cn('inline-block size-2 rounded-full', status.dotClass)}
          aria-hidden
        />
        <span>{status.label}</span>
      </div>
      <ChevronRight className="size-4 text-muted-foreground" aria-hidden />
    </Link>
  );
}

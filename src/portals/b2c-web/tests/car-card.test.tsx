// CarCard unit tests — Plan 04-04 / Task 3a <behavior>.
//
// Assertions:
//   - Renders vendor name, category, transmission, seats, bags, pickup location,
//     daily rate + "/day" suffix, total + currency, and the cancellation badge
//     with VERBATIM UI-SPEC copy ("Free cancellation" | "Non-refundable" | "Flexible").
//
// We stub next/image to a plain <img> so jsdom doesn't complain about the optimizer
// contract (same pattern as hotel-card.test.tsx).

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CarCard } from '@/components/results/car-card';
import type { CarOffer } from '@/types/car';

vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

function offer(partial: Partial<CarOffer> = {}): CarOffer {
  return {
    offerId: 'CO-1',
    vendorName: 'Avis',
    vendorLogo: 'https://img.example/avis.png',
    category: 'Compact',
    transmission: 'automatic',
    seats: 5,
    bags: 2,
    pickupLocation: 'LHR Terminal 5',
    dropoffLocation: 'LHR Terminal 5',
    pickupAt: '2026-05-01T10:00:00Z',
    dropoffAt: '2026-05-04T10:00:00Z',
    dailyRate: { amount: 63, currency: 'GBP' },
    totalAmount: { amount: 189, currency: 'GBP' },
    cancellationPolicy: 'free',
    ...partial,
  };
}

describe('<CarCard>', () => {
  it('renders vendor name, category, seats, bags, pickup location, and "Free cancellation" badge', () => {
    render(<CarCard offer={offer()} />);

    expect(screen.getByText('Avis')).toBeInTheDocument();
    expect(screen.getByText(/Compact/)).toBeInTheDocument();
    expect(screen.getByText(/LHR Terminal 5/)).toBeInTheDocument();
    expect(screen.getByTestId('seats-chip')).toHaveTextContent('5 seats');
    expect(screen.getByTestId('bags-chip')).toHaveTextContent('2 bags');
    expect(screen.getByTestId('transmission-chip')).toHaveTextContent('Automatic');
    expect(screen.getByTestId('cancellation-badge')).toHaveTextContent('Free cancellation');
  });

  it('shows daily rate with "/day" suffix and total with currency', () => {
    render(<CarCard offer={offer()} />);

    // Intl.NumberFormat for GBP produces "£63.00" (en-GB).
    expect(screen.getByText((t) => t.includes('£63.00'))).toBeInTheDocument();
    expect(screen.getByText('/day')).toBeInTheDocument();
    expect(
      screen.getByText((t) => t.includes('£189.00') && t.includes('total')),
    ).toBeInTheDocument();
  });

  it('renders vendor logo with alt = vendor name', () => {
    render(<CarCard offer={offer()} />);
    expect(screen.getByRole('img', { name: 'Avis' })).toBeInTheDocument();
  });

  it('maps manual transmission to "Manual" chip', () => {
    render(<CarCard offer={offer({ transmission: 'manual' })} />);
    expect(screen.getByTestId('transmission-chip')).toHaveTextContent('Manual');
  });

  it('maps non-refundable policy to "Non-refundable" badge', () => {
    render(<CarCard offer={offer({ cancellationPolicy: 'nonRefundable' })} />);
    expect(screen.getByTestId('cancellation-badge')).toHaveTextContent('Non-refundable');
  });
});

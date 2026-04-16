// HotelCard unit tests — Plan 04-03 / Task 3 <behavior>.
//
// Assertions:
//   - Renders property name, star rating (aria-label), cancellation badge
//     with VERBATIM UI-SPEC copy ("Free cancellation" | "Non-refundable"
//     | "Flexible"), nightly rate formatted + " / night" suffix, total
//     formatted with currency, and the photo carries alt = property name.
//
// We stub next/image to a plain <img> so jsdom doesn't complain about
// the optimizer contract (same pattern used elsewhere in the suite).

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { HotelCard } from '@/components/results/hotel-card';
import type { HotelOffer } from '@/types/hotel';

// Minimal next/image stub — returns a plain <img> tag.
vi.mock('next/image', () => ({
  default: ({ src, alt }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} />
  ),
}));

function offer(partial: Partial<HotelOffer> = {}): HotelOffer {
  return {
    offerId: 'OF-1',
    propertyId: 'PROP-1',
    name: 'The Grand Sample',
    starRating: 4,
    address: '1 Example Street, London',
    photos: ['https://img.example/hotel.jpg'],
    amenities: ['WiFi', 'Pool'],
    cancellationPolicy: 'free',
    rooms: [],
    nightlyRate: { amount: 152, currency: 'GBP' },
    totalAmount: { amount: 456, currency: 'GBP' },
    ...partial,
  };
}

describe('<HotelCard>', () => {
  it('renders property name, address, stars, photo alt, and "Free cancellation" badge', () => {
    render(<HotelCard offer={offer()} />);

    expect(screen.getByText('The Grand Sample')).toBeInTheDocument();
    expect(screen.getByText('1 Example Street, London')).toBeInTheDocument();
    expect(
      screen.getByRole('img', { name: 'The Grand Sample' }),
    ).toBeInTheDocument();
    expect(
      screen.getByLabelText(/4-star property/i),
    ).toBeInTheDocument();
    expect(screen.getByTestId('cancellation-badge')).toHaveTextContent(
      'Free cancellation',
    );
  });

  it('shows nightly rate with "/night" suffix and total with currency', () => {
    render(<HotelCard offer={offer()} />);

    // Intl.NumberFormat for GBP produces "£152.00" (en-GB).
    expect(screen.getByText((t) => t.includes('£152.00'))).toBeInTheDocument();
    // "/night" suffix is asserted directly per Plan 04-03 acceptance.
    expect(screen.getByText('/night')).toBeInTheDocument();
    // Total line carries the formatted total plus " total".
    expect(
      screen.getByText((t) => t.includes('£456.00') && t.includes('total')),
    ).toBeInTheDocument();
  });

  it('maps non-refundable policy to "Non-refundable" badge', () => {
    render(<HotelCard offer={offer({ cancellationPolicy: 'nonRefundable' })} />);
    expect(screen.getByTestId('cancellation-badge')).toHaveTextContent(
      'Non-refundable',
    );
  });

  it('maps flexible policy to "Flexible" badge', () => {
    render(<HotelCard offer={offer({ cancellationPolicy: 'flexible' })} />);
    expect(screen.getByTestId('cancellation-badge')).toHaveTextContent('Flexible');
  });
});

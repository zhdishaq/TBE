// HotelSearchForm unit tests — Plan 04-03 / Task 3 <behavior>.
//
// The form's validator is exposed as a pure function
// (`validateHotelSearch`) so we can assert the zod-equivalent rules
// without mounting RHF / the entire component tree.
//
// The form-submit navigation test uses the router mock to capture the
// push target — same pattern the flights form test uses — and the
// actual component render path is a light smoke assertion.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HotelSearchForm, validateHotelSearch } from '@/components/search/hotel-search-form';

// Mock next/navigation's useRouter so submit can capture the push URL.
const pushSpy = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushSpy, replace: vi.fn(), back: vi.fn() }),
}));

// nuqs' createSerializer works fine in jsdom — no mock needed.

describe('validateHotelSearch', () => {
  const goodRange = {
    from: new Date('2099-05-01T00:00:00Z'),
    to: new Date('2099-05-04T00:00:00Z'),
  };
  const goodDestination = {
    cityCode: 'LON',
    city: 'London',
    region: 'Greater London',
    country: 'GB',
  };
  const goodOccupancy = { rooms: 1, adults: 2, children: 0 };

  it('rejects a missing destination', () => {
    const errs = validateHotelSearch({
      destination: null,
      range: goodRange,
      occupancy: goodOccupancy,
    });
    expect(errs.some((e) => e.field === 'destination')).toBe(true);
  });

  it('rejects a destination shorter than 2 chars', () => {
    const errs = validateHotelSearch({
      destination: { ...goodDestination, cityCode: 'L' },
      range: goodRange,
      occupancy: goodOccupancy,
    });
    expect(errs.some((e) => e.field === 'destination')).toBe(true);
  });

  it('rejects checkout on or before checkin', () => {
    const errs = validateHotelSearch({
      destination: goodDestination,
      range: {
        from: new Date('2099-05-04T00:00:00Z'),
        to: new Date('2099-05-04T00:00:00Z'),
      },
      occupancy: goodOccupancy,
    });
    expect(errs.some((e) => e.field === 'dates')).toBe(true);
  });

  it('rejects adults outside 1-9', () => {
    expect(
      validateHotelSearch({
        destination: goodDestination,
        range: goodRange,
        occupancy: { rooms: 1, adults: 0, children: 0 },
      }).some((e) => e.field === 'occupancy'),
    ).toBe(true);
    expect(
      validateHotelSearch({
        destination: goodDestination,
        range: goodRange,
        occupancy: { rooms: 1, adults: 10, children: 0 },
      }).some((e) => e.field === 'occupancy'),
    ).toBe(true);
  });

  it('rejects children outside 0-4', () => {
    expect(
      validateHotelSearch({
        destination: goodDestination,
        range: goodRange,
        occupancy: { rooms: 1, adults: 2, children: 5 },
      }).some((e) => e.field === 'occupancy'),
    ).toBe(true);
  });

  it('accepts a valid combination (1-9 adults, 0-4 children)', () => {
    expect(
      validateHotelSearch({
        destination: goodDestination,
        range: goodRange,
        occupancy: { rooms: 2, adults: 9, children: 4 },
      }),
    ).toEqual([]);
  });
});

describe('<HotelSearchForm>', () => {
  beforeEach(() => {
    pushSpy.mockReset();
  });

  it('renders the "Search hotels" submit button', () => {
    render(<HotelSearchForm />);
    expect(
      screen.getByRole('button', { name: /search hotels/i }),
    ).toBeInTheDocument();
  });

  it('does NOT navigate when destination is missing', async () => {
    const user = userEvent.setup();
    render(<HotelSearchForm />);
    await user.click(screen.getByRole('button', { name: /search hotels/i }));
    expect(pushSpy).not.toHaveBeenCalled();
    // Validation error surface appears.
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});

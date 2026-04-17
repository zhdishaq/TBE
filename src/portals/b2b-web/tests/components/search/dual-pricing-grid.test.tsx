// Plan 05-02 Task 3 RED tests -- DualPricingGrid.
//
// Verifies the UI-SPEC locked contract:
//   - Renders FOUR price columns (NET, Markup, GROSS, Commission).
//   - Every price cell carries `tabular-nums` AND an `aria-label` declaring
//     the pricing role (e.g. "Net fare").
//   - The Commission column (and only that column) is green-700.
//   - Sort dropdown exposes `commission-asc` AND `commission-desc`.
//   - Selecting a row applies the indigo-600 left border + ring-indigo-200
//     selection treatment.

import { describe, it, expect, vi } from 'vitest';
import { render, screen, within, fireEvent } from '@testing-library/react';
import { DualPricingGrid, type PricedOffer } from '@/app/(portal)/search/flights/dual-pricing-grid';

const offers: PricedOffer[] = [
  {
    offerId: 'o1',
    airline: 'BA',
    flightNumber: 'BA24',
    departAt: '2026-05-01T08:00:00Z',
    arriveAt: '2026-05-01T11:10:00Z',
    durationMinutes: 190,
    stops: 0,
    net: 200,
    markup: 30,
    gross: 230,
    commission: 30,
    currency: 'GBP',
  },
  {
    offerId: 'o2',
    airline: 'BA',
    flightNumber: 'BA26',
    departAt: '2026-05-01T14:00:00Z',
    arriveAt: '2026-05-01T17:20:00Z',
    durationMinutes: 200,
    stops: 0,
    net: 210,
    markup: 25,
    gross: 235,
    commission: 25,
    currency: 'GBP',
  },
];

describe('DualPricingGrid', () => {
  it('renders a header row with all FOUR pricing columns', () => {
    render(<DualPricingGrid offers={offers} />);
    expect(screen.getByRole('columnheader', { name: /^NET$/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /^Markup$/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /^GROSS$/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /^Commission$/i })).toBeInTheDocument();
  });

  it('gives every price cell an aria-label declaring the pricing role', () => {
    render(<DualPricingGrid offers={offers} />);
    // Each offer contributes four aria-labels -> two offers => two of each.
    expect(screen.getAllByLabelText(/Net fare/i)).toHaveLength(2);
    expect(screen.getAllByLabelText(/Markup/i)).toHaveLength(2);
    expect(screen.getAllByLabelText(/Gross fare/i)).toHaveLength(2);
    expect(screen.getAllByLabelText(/Agency commission/i)).toHaveLength(2);
  });

  it('applies tabular-nums to every price cell', () => {
    render(<DualPricingGrid offers={offers} />);
    const commissionCells = screen.getAllByLabelText(/Agency commission/i);
    for (const cell of commissionCells) {
      expect(cell.className).toContain('tabular-nums');
    }
    const netCells = screen.getAllByLabelText(/Net fare/i);
    for (const cell of netCells) {
      expect(cell.className).toContain('tabular-nums');
    }
  });

  it('colours ONLY the Commission column with text-green-700', () => {
    render(<DualPricingGrid offers={offers} />);
    const commissionCells = screen.getAllByLabelText(/Agency commission/i);
    for (const cell of commissionCells) {
      expect(cell.className).toContain('text-green-700');
    }
    const netCells = screen.getAllByLabelText(/Net fare/i);
    for (const cell of netCells) {
      expect(cell.className).not.toContain('text-green-700');
    }
  });

  it('exposes commission-asc AND commission-desc sort keys', () => {
    render(<DualPricingGrid offers={offers} />);
    const select = screen.getByLabelText(/Sort offers/i) as HTMLSelectElement;
    const values = Array.from(select.options).map((o) => o.value);
    expect(values).toContain('commission-asc');
    expect(values).toContain('commission-desc');
  });

  it('applies indigo-600 left border + ring treatment to the selected row', () => {
    const onSelect = vi.fn();
    render(<DualPricingGrid offers={offers} onSelect={onSelect} />);
    const selectButtons = screen.getAllByRole('button', { name: /select/i });
    fireEvent.click(selectButtons[0]);
    expect(onSelect).toHaveBeenCalledWith('o1');
    const row = screen.getByTestId('offer-row-o1');
    expect(row.className).toMatch(/border-l-4/);
    expect(row.className).toMatch(/border-l-indigo-600/);
    expect(row.className).toMatch(/ring-indigo-200/);
  });

  it('sorts by commission when commission-desc is selected', () => {
    render(<DualPricingGrid offers={offers} initialSort="commission-desc" />);
    const tbody = screen.getByRole('table').querySelector('tbody')!;
    const rows = within(tbody).getAllByRole('row');
    // `o1` has commission 30, `o2` has 25 -> descending => o1 first.
    expect(rows[0].getAttribute('data-testid')).toBe('offer-row-o1');
    expect(rows[1].getAttribute('data-testid')).toBe('offer-row-o2');
  });
});

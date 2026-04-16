// Plan 04-01 Task 2 — account EmptyState primitive.
//
// EmptyState is the shared shell for dashboard, results and trip-builder
// zero-state surfaces. The copy for each surface is locked by UI-SPEC and
// must render verbatim when the component is fed those exact props.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EmptyState } from '@/components/account/empty-state';

describe('EmptyState', () => {
  it('renders heading + body verbatim', () => {
    render(
      <EmptyState
        heading="No upcoming trips"
        body="When you book a flight, hotel, or car, it will appear here. Search now to get started."
      />,
    );

    expect(screen.getByText('No upcoming trips')).toBeInTheDocument();
    expect(
      screen.getByText(
        'When you book a flight, hotel, or car, it will appear here. Search now to get started.',
      ),
    ).toBeInTheDocument();
  });

  it('renders an action link when the action prop is provided', () => {
    render(
      <EmptyState
        heading="No upcoming trips"
        body="When you book a flight, hotel, or car, it will appear here. Search now to get started."
        action={{ href: '/', label: 'Start a search' }}
      />,
    );

    const link = screen.getByRole('link', { name: 'Start a search' });
    expect(link).toHaveAttribute('href', '/');
  });

  it('renders the past-bookings copy when fed the past-bookings props', () => {
    render(
      <EmptyState
        heading="No past bookings yet"
        body="Your booking history will show here once you have completed a trip."
      />,
    );

    expect(screen.getByText('No past bookings yet')).toBeInTheDocument();
    expect(
      screen.getByText(
        'Your booking history will show here once you have completed a trip.',
      ),
    ).toBeInTheDocument();
  });

  it('omits the action when no action prop is provided', () => {
    render(
      <EmptyState
        heading="No past bookings yet"
        body="Your booking history will show here once you have completed a trip."
      />,
    );

    expect(screen.queryByRole('link')).toBeNull();
  });
});

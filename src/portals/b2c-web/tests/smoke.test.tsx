// Wave 0 unit smoke — proves that jsdom + React Testing Library +
// tsconfig `paths` all agree. If this fails, every other frontend
// test in phase 4 fails too.

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

// next/navigation needs an App Router context at render time; stub it
// with inert fakes so the home page (which embeds <FlightSearchForm>)
// can mount in a jsdom unit test.
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
  }),
  usePathname: () => '/',
  useSearchParams: () => new URLSearchParams(),
}));

// nuqs (indirectly consumed by form components in later plans) also
// needs a client router context — stub it away for the smoke render.
vi.mock('nuqs', () => ({
  useQueryStates: () => [
    {
      from: '',
      to: '',
      dep: null,
      ret: null,
      adt: 1,
      chd: 0,
      infl: 0,
      infs: 0,
      cabin: 'economy',
      stops: null,
      airlines: null,
      timeWindow: null,
      price: null,
      sort: 'price',
    },
    () => Promise.resolve(null),
  ],
  createSerializer: () => () => '',
}));

import HomePage from '@/app/page';

describe('landing page (unit smoke)', () => {
  it('renders the landing heading and embedded search CTA', () => {
    render(<HomePage />);
    expect(
      screen.getByRole('heading', { name: /book your trip/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /search flights/i }),
    ).toBeInTheDocument();
  });
});

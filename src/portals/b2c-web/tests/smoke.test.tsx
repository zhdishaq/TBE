// Wave 0 unit smoke — proves that jsdom + React Testing Library +
// tsconfig `paths` all agree. If this fails, every other frontend
// test in phase 4 fails too.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import HomePage from '@/app/page';

describe('landing page (unit smoke)', () => {
  it('renders placeholder heading', () => {
    render(<HomePage />);
    expect(
      screen.getByRole('heading', { name: /book your trip/i }),
    ).toBeInTheDocument();
  });
});

// Wave 0 unit smoke — proves jsdom + React Testing Library + tsconfig
// path alias all agree for the B2B portal. If this fails, every other
// frontend test in Phase 5 fails too.
//
// Source: 05-00-PLAN Task 2 + fork of b2c-web/tests/smoke.test.tsx.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import HomePage from '@/app/page';

describe('B2B landing page (unit smoke)', () => {
  it('renders the agent portal heading', () => {
    render(<HomePage />);
    expect(
      screen.getByRole('heading', { name: /agent portal/i }),
    ).toBeInTheDocument();
  });
});

// Plan 05-05 Task 5 — RequestTopUpLink.
//
// Facts cover (from 05-05-PLAN Task 5 behavior block):
//   1. Renders <a href="mailto:{adminEmail}?subject=Top-up%20request">.
//   2. href contains NO body= param, NO session material (agency_id, token,
//      balance, threshold) — T-05-03-09 / T-05-05-02 mitigation codified.
//   3. Link text literally "Request top-up" with rel="noopener noreferrer".

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RequestTopUpLink } from '@/components/wallet/request-top-up-link';

describe('RequestTopUpLink', () => {
  it('renders mailto with subject and explicit adminEmail prop', () => {
    render(<RequestTopUpLink adminEmail="owner@acme.test" />);
    const link = screen.getByRole('link', { name: /request top-up/i });
    const href = link.getAttribute('href') ?? '';
    expect(href.startsWith('mailto:owner@acme.test')).toBe(true);
    expect(href).toContain('subject=');
    expect(href.toLowerCase()).toContain('top-up');
  });

  it('href carries ONLY subject — no body, no agency_id, no token, no balance, no threshold (T-05-03-09)', () => {
    render(<RequestTopUpLink adminEmail="owner@acme.test" />);
    const href = screen.getByRole('link', { name: /request top-up/i })
      .getAttribute('href') ?? '';
    // Subject parameter present.
    expect(href).toMatch(/\?subject=/);
    // Nothing else. In particular: no body=, no agency, no balance, no token.
    expect(href).not.toMatch(/body=/i);
    expect(href).not.toMatch(/agency_?id/i);
    expect(href).not.toMatch(/token/i);
    expect(href).not.toMatch(/sid/i);
    expect(href).not.toMatch(/balance/i);
    expect(href).not.toMatch(/threshold/i);
    expect(href).not.toMatch(/session/i);
  });

  it('link text is literally "Request top-up" with rel="noopener noreferrer"', () => {
    render(<RequestTopUpLink adminEmail="owner@acme.test" />);
    const link = screen.getByRole('link', { name: /request top-up/i });
    expect(link.textContent?.trim()).toBe('Request top-up');
    expect(link.getAttribute('rel')).toBe('noopener noreferrer');
  });
});

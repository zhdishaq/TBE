// Plan 05-02 Task 3 RED tests -- CheckoutDetailsForm.
//
// Admin gating for the AgencyMarkupOverride field is UX polish only
// (the server enforces the actual gate), but the client-side contract
// MUST be: only agent-admin roles see the override input. Any other role
// (agent, agent-readonly, empty roles) gets the form WITHOUT the override
// field so a non-admin cannot enter a value in the first place.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CheckoutDetailsForm } from '@/app/(portal)/checkout/details/checkout-details-form';

describe('CheckoutDetailsForm', () => {
  it('renders the Agency markup override field ONLY for agent-admin roles', () => {
    render(<CheckoutDetailsForm roles={['agent-admin']} />);
    expect(screen.getByLabelText(/Agency markup override/i)).toBeInTheDocument();
  });

  it('HIDES the override field for a plain agent', () => {
    render(<CheckoutDetailsForm roles={['agent']} />);
    expect(screen.queryByLabelText(/Agency markup override/i)).toBeNull();
  });

  it('HIDES the override field for agent-readonly (D-35 write gate)', () => {
    render(<CheckoutDetailsForm roles={['agent-readonly']} />);
    expect(screen.queryByLabelText(/Agency markup override/i)).toBeNull();
  });

  it('renders the passenger + customer-contact fieldsets for every role', () => {
    render(<CheckoutDetailsForm roles={['agent']} />);
    expect(screen.getByLabelText(/Passenger 1 first name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Passenger 1 last name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Passenger 1 date of birth/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Customer contact/i).length).toBeGreaterThan(0);
  });
});

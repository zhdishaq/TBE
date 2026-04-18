// Plan 05-05 Task 4 — ThresholdDialog (Radix Dialog + zod + optimistic mutation).
//
// Facts cover (from 05-05-PLAN Task 4 behavior block):
//   1. Dialog opens on trigger click (Radix Dialog — NOT AlertDialog; D-44
//      reserves AlertDialog for destructive actions only).
//   2. Input prefills from queryClient.getQueryData(['wallet','threshold']).
//   3. On save, calls PUT /api/wallet/threshold with {thresholdAmount,currency},
//      optimistically updates ['wallet','threshold'], invalidates
//      ['wallet','balance'] + ['wallet','transactions'].
//   4. zod clamp £50-£10,000 — submitting £25 shows inline error, no PUT.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ThresholdDialog } from '@/app/(portal)/admin/wallet/threshold-dialog';

function renderWithClient(
  ui: React.ReactElement,
  seed?: (qc: QueryClient) => void,
) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnMount: false } },
  });
  if (seed) seed(client);
  return {
    client,
    ...render(
      <QueryClientProvider client={client}>{ui}</QueryClientProvider>,
    ),
  };
}

describe('ThresholdDialog', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
  });

  it('opens the Radix Dialog when the trigger is clicked', async () => {
    renderWithClient(<ThresholdDialog />);
    const trigger = screen.getByRole('button', { name: /edit threshold/i });
    await userEvent.click(trigger);
    expect(
      await screen.findByRole('dialog', { name: /threshold/i }),
    ).toBeInTheDocument();
  });

  it('prefills the input from ["wallet","threshold"] cache', async () => {
    renderWithClient(<ThresholdDialog />, (qc) =>
      qc.setQueryData(['wallet', 'threshold'], { threshold: 750, currency: 'GBP' }),
    );
    await userEvent.click(screen.getByRole('button', { name: /edit threshold/i }));
    const input = (await screen.findByLabelText(/threshold/i)) as HTMLInputElement;
    expect(input.value).toBe('750');
  });

  it('PUTs the new threshold and invalidates wallet queries on success', async () => {
    const fetchSpy = vi.fn().mockResolvedValue({
      ok: true,
      status: 204,
      headers: new Headers({ 'content-type': 'application/json' }),
      json: async () => ({}),
    });
    vi.stubGlobal('fetch', fetchSpy);

    const { client } = renderWithClient(<ThresholdDialog />, (qc) =>
      qc.setQueryData(['wallet', 'threshold'], { threshold: 500, currency: 'GBP' }),
    );
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries');

    await userEvent.click(screen.getByRole('button', { name: /edit threshold/i }));
    const input = (await screen.findByLabelText(/threshold/i)) as HTMLInputElement;
    await userEvent.clear(input);
    await userEvent.type(input, '1000');
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledWith(
        '/api/wallet/threshold',
        expect.objectContaining({ method: 'PUT' }),
      );
    });
    // Body has thresholdAmount + currency (never agencyId — Pitfall 28).
    const [[, init]] = fetchSpy.mock.calls;
    const body = JSON.parse((init as RequestInit).body as string);
    expect(body.thresholdAmount).toBe(1000);
    expect(body.currency).toBe('GBP');
    expect(body.agencyId).toBeUndefined();

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalled();
    });
  });

  it('zod: submitting £25 shows inline error and does NOT PUT', async () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);

    renderWithClient(<ThresholdDialog />, (qc) =>
      qc.setQueryData(['wallet', 'threshold'], { threshold: 500, currency: 'GBP' }),
    );
    await userEvent.click(screen.getByRole('button', { name: /edit threshold/i }));
    const input = (await screen.findByLabelText(/threshold/i)) as HTMLInputElement;
    await userEvent.clear(input);
    await userEvent.type(input, '25');
    fireEvent.submit(input.closest('form')!);

    await waitFor(() => {
      expect(
        screen.getByText(/must be between £50 and £10 ?000/i),
      ).toBeInTheDocument();
    });
    expect(fetchSpy).not.toHaveBeenCalled();
  });
});

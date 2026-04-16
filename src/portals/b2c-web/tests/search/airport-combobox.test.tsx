// Task 2 RED/GREEN — behaviours from 04-02-PLAN.md §Task 2 <behavior>.
//
// The AirportCombobox MUST:
//   - debounce 200ms before fetching
//   - require at least 2 chars
//   - render results in `"{IATA} — {name}"` format
//   - abort a previous fetch when a new keystroke fires (AbortController)
//
// We use real timers + `waitFor` so user-event's internal scheduling
// plays nicely (fake timers + user-event v14 has known interaction
// issues — the test intent is behaviour, not sub-ms precision).

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AirportCombobox } from '@/components/search/airport-combobox';

type FetchMock = ReturnType<typeof vi.fn>;

/** Build a fetch that resolves immediately with a canned payload but respects AbortSignal. */
function makeSpyFetch(payload: unknown): FetchMock {
  return vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
    const signal = init?.signal;
    return new Promise<Response>((resolve, reject) => {
      if (signal?.aborted) {
        const err = new Error('aborted');
        err.name = 'AbortError';
        reject(err);
        return;
      }
      signal?.addEventListener('abort', () => {
        const err = new Error('aborted');
        err.name = 'AbortError';
        reject(err);
      });
      // Resolve on the next microtask so the caller sees "pending" first.
      queueMicrotask(() => {
        if (signal?.aborted) return;
        resolve(
          new Response(JSON.stringify(payload), {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          }),
        );
      });
    });
  });
}

describe('<AirportCombobox>', () => {
  let originalFetch: typeof fetch;

  beforeEach(() => {
    originalFetch = global.fetch;
  });

  afterEach(() => {
    global.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('does NOT fetch until the user has typed at least 2 characters', async () => {
    const fetchSpy = makeSpyFetch([]);
    global.fetch = fetchSpy as unknown as typeof fetch;

    const user = userEvent.setup();
    render(<AirportCombobox label="From" value={null} onChange={() => {}} />);
    const input = screen.getByRole('combobox', { name: /from/i });

    await user.click(input);
    await user.type(input, 'l'); // 1 char
    // Wait enough for the 200ms debounce to have fired if it were going to.
    await new Promise((r) => setTimeout(r, 280));

    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('debounces 200ms and fires exactly one request for a stable 2-char query', async () => {
    const fetchSpy = makeSpyFetch([
      { iata: 'LHR', name: 'London Heathrow', city: 'London', country: 'GB' },
    ]);
    global.fetch = fetchSpy as unknown as typeof fetch;

    const user = userEvent.setup();
    render(<AirportCombobox label="From" value={null} onChange={() => {}} />);
    const input = screen.getByRole('combobox', { name: /from/i });

    await user.click(input);
    await user.type(input, 'lo');

    // Wait past the 200ms debounce and assert exactly one call went out.
    await waitFor(() => expect(fetchSpy).toHaveBeenCalledTimes(1), { timeout: 1500 });
  });

  it('renders results in "{IATA} — {name}" format', async () => {
    const fetchSpy = makeSpyFetch([
      { iata: 'LHR', name: 'London Heathrow', city: 'London', country: 'GB' },
    ]);
    global.fetch = fetchSpy as unknown as typeof fetch;

    const user = userEvent.setup();
    render(<AirportCombobox label="From" value={null} onChange={() => {}} />);
    const input = screen.getByRole('combobox', { name: /from/i });

    await user.click(input);
    await user.type(input, 'lon');

    await waitFor(
      () => expect(screen.getByText(/LHR\s*—\s*London Heathrow/)).toBeInTheDocument(),
      { timeout: 1500 },
    );
  });

  it('aborts a prior inflight request when a new keystroke fires', async () => {
    const fetchSpy = makeSpyFetch([]);
    global.fetch = fetchSpy as unknown as typeof fetch;

    const user = userEvent.setup();
    render(<AirportCombobox label="From" value={null} onChange={() => {}} />);
    const input = screen.getByRole('combobox', { name: /from/i });

    await user.click(input);
    await user.type(input, 'lo');
    await waitFor(() => expect(fetchSpy).toHaveBeenCalledTimes(1), { timeout: 1500 });
    const firstInit = fetchSpy.mock.calls[0][1] as RequestInit;
    expect(firstInit.signal).toBeInstanceOf(AbortSignal);

    // Type another char — should fire request #2 AND abort request #1.
    await user.type(input, 'n');
    await waitFor(() => expect(fetchSpy).toHaveBeenCalledTimes(2), { timeout: 1500 });
    expect((firstInit.signal as AbortSignal).aborted).toBe(true);
  });
});

// Task 2 RED — behaviours from 04-02-PLAN.md §Task 2 <behavior>.
//
// The AirportCombobox MUST:
//   - debounce 200ms before fetching
//   - require at least 2 chars
//   - render results in `"{IATA} — {name}"` format
//   - abort a previous fetch when a new keystroke fires (AbortController)

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AirportCombobox } from '@/components/search/airport-combobox';

type FetchMock = ReturnType<typeof vi.fn>;

// Produce a fetch that resolves with a canned payload but respects AbortSignal
// (throws AbortError if the signal fires before the internal timeout resolves).
function makeSpyFetch(payload: unknown): FetchMock {
  return vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const signal = init?.signal;
    return new Promise<Response>((resolve, reject) => {
      const t = setTimeout(() => {
        resolve(
          new Response(JSON.stringify(payload), {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          }),
        );
      }, 10);
      if (signal) {
        signal.addEventListener('abort', () => {
          clearTimeout(t);
          const err = new Error('aborted');
          err.name = 'AbortError';
          reject(err);
        });
      }
    });
  });
}

describe('<AirportCombobox>', () => {
  let originalFetch: typeof fetch;

  beforeEach(() => {
    vi.useFakeTimers();
    originalFetch = global.fetch;
  });

  afterEach(() => {
    vi.useRealTimers();
    global.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('does NOT fetch until the user has typed at least 2 characters', async () => {
    const fetchSpy = makeSpyFetch([]);
    global.fetch = fetchSpy as unknown as typeof fetch;

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<AirportCombobox label="From" value={null} onChange={() => {}} />);
    const input = screen.getByRole('combobox', { name: /from/i });

    await user.click(input);
    await user.type(input, 'l'); // 1 char
    // advance past the 200ms debounce window
    vi.advanceTimersByTime(250);

    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('debounces 200ms and fires exactly one request for a stable 2-char query', async () => {
    const fetchSpy = makeSpyFetch([
      { iata: 'LHR', name: 'London Heathrow', city: 'London', country: 'GB' },
    ]);
    global.fetch = fetchSpy as unknown as typeof fetch;

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<AirportCombobox label="From" value={null} onChange={() => {}} />);
    const input = screen.getByRole('combobox', { name: /from/i });

    await user.click(input);
    await user.type(input, 'lo');
    vi.advanceTimersByTime(199); // under threshold — should NOT have fired
    expect(fetchSpy).not.toHaveBeenCalled();
    vi.advanceTimersByTime(50); // cross the 200ms boundary
    await waitFor(() => expect(fetchSpy).toHaveBeenCalledTimes(1));
  });

  it('renders results in "{IATA} — {name}" format', async () => {
    const fetchSpy = makeSpyFetch([
      { iata: 'LHR', name: 'London Heathrow', city: 'London', country: 'GB' },
    ]);
    global.fetch = fetchSpy as unknown as typeof fetch;

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<AirportCombobox label="From" value={null} onChange={() => {}} />);
    const input = screen.getByRole('combobox', { name: /from/i });

    await user.click(input);
    await user.type(input, 'lon');
    vi.advanceTimersByTime(250);
    await vi.runAllTimersAsync();

    await waitFor(() =>
      expect(screen.getByText(/LHR\s*—\s*London Heathrow/)).toBeInTheDocument(),
    );
  });

  it('aborts a prior inflight request when a new keystroke fires', async () => {
    const fetchSpy = makeSpyFetch([]);
    global.fetch = fetchSpy as unknown as typeof fetch;

    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    render(<AirportCombobox label="From" value={null} onChange={() => {}} />);
    const input = screen.getByRole('combobox', { name: /from/i });

    await user.click(input);
    await user.type(input, 'lo');
    vi.advanceTimersByTime(250); // kicks off request #1
    await waitFor(() => expect(fetchSpy).toHaveBeenCalledTimes(1));
    const firstInit = fetchSpy.mock.calls[0][1] as RequestInit;
    expect(firstInit.signal).toBeInstanceOf(AbortSignal);

    // Type another char — should fire request #2 AND abort request #1
    await user.type(input, 'n');
    vi.advanceTimersByTime(250);
    await waitFor(() => expect(fetchSpy).toHaveBeenCalledTimes(2));
    expect((firstInit.signal as AbortSignal).aborted).toBe(true);
  });
});

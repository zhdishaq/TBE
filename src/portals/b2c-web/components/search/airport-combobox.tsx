'use client';

// IATA airport combobox (B2C-03).
//
// - cmdk <Command> wrapped by a Radix Popover trigger (UI-SPEC).
// - 200ms debounce before firing a fetch (Pitfall 10).
// - Minimum 2 characters before we query; 1-char queries are ignored so
//   we don't spam the 60/min rate-limit budget.
// - AbortController cancels a prior in-flight fetch on keystroke so
//   stale results can never overwrite fresh ones (Pitfall 10, T-04-02-04).
// - Renders items as "{IATA} — {name}".
//
// The component is intentionally controlled — the parent form owns the
// selected AirportOption and provides `onChange`. Keeping it controlled
// is what lets react-hook-form validate the form without leaking the
// combobox internals.

import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { ChevronsUpDown, MapPin, Search } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface AirportOption {
  iata: string;
  name: string;
  city: string;
  country: string;
}

export interface AirportComboboxProps {
  label: string;
  value: AirportOption | null;
  onChange: (next: AirportOption | null) => void;
  placeholder?: string;
  className?: string;
  disabled?: boolean;
}

const MIN_QUERY_LEN = 2;
const DEBOUNCE_MS = 200;

export function AirportCombobox({
  label,
  value,
  onChange,
  placeholder = 'City or airport',
  className,
  disabled,
}: AirportComboboxProps) {
  const inputId = useId();
  const listboxId = useId();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<AirportOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [highlightIndex, setHighlightIndex] = useState(0);
  const abortRef = useRef<AbortController | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // --- Debounced fetch on query change --------------------------------
  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
      debounceRef.current = null;
    }

    if (query.length < MIN_QUERY_LEN) {
      // Abort any inflight request AND clear the results list so the
      // previous hits don't linger once the user deletes back to 1 char.
      abortRef.current?.abort();
      setResults([]);
      setLoading(false);
      return;
    }

    debounceRef.current = setTimeout(() => {
      // Cancel previous request before firing a new one.
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;
      setLoading(true);
      fetch(
        `/api/airports?q=${encodeURIComponent(query)}&limit=10`,
        { signal: controller.signal },
      )
        .then(async (r) => {
          if (!r.ok) return [] as AirportOption[];
          return (await r.json()) as AirportOption[];
        })
        .then((data) => {
          // Guard against late resolves from an aborted request.
          if (controller.signal.aborted) return;
          setResults(Array.isArray(data) ? data : []);
          setHighlightIndex(0);
        })
        .catch((err: unknown) => {
          if ((err as Error)?.name === 'AbortError') return;
          setResults([]);
        })
        .finally(() => {
          if (!controller.signal.aborted) setLoading(false);
        });
    }, DEBOUNCE_MS);

    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [query]);

  // Abort any inflight request on unmount so we don't leak work.
  useEffect(() => () => abortRef.current?.abort(), []);

  const selectedLabel = useMemo(() => {
    if (!value) return '';
    return `${value.iata} — ${value.name}`;
  }, [value]);

  function handleSelect(opt: AirportOption) {
    onChange(opt);
    setQuery(`${opt.iata} — ${opt.name}`);
    setOpen(false);
  }

  function handleKey(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setHighlightIndex((i) => Math.min(i + 1, results.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlightIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === 'Enter' && results[highlightIndex]) {
      e.preventDefault();
      handleSelect(results[highlightIndex]);
    } else if (e.key === 'Escape') {
      setOpen(false);
    }
  }

  return (
    <div className={cn('flex flex-col gap-1', className)}>
      <label htmlFor={inputId} className="text-sm font-medium text-muted-foreground">
        {label}
      </label>
      <div className="relative">
        <div className="pointer-events-none absolute inset-y-0 start-3 flex items-center">
          <MapPin size={16} className="text-muted-foreground" />
        </div>
        <input
          id={inputId}
          role="combobox"
          aria-label={label}
          aria-autocomplete="list"
          aria-expanded={open}
          aria-controls={listboxId}
          aria-activedescendant={
            results[highlightIndex]
              ? `${listboxId}-opt-${results[highlightIndex].iata}`
              : undefined
          }
          placeholder={placeholder}
          value={open ? query : selectedLabel || query}
          disabled={disabled}
          onChange={(e) => {
            setQuery(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          onBlur={() => {
            // Give the click handler a tick to fire before closing.
            setTimeout(() => setOpen(false), 150);
          }}
          onKeyDown={handleKey}
          className={cn(
            'w-full rounded-md border border-input bg-background py-2 pe-10 ps-9 text-sm outline-none ring-offset-background',
            'focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
            'disabled:cursor-not-allowed disabled:opacity-50',
          )}
        />
        <div className="pointer-events-none absolute inset-y-0 end-3 flex items-center text-muted-foreground">
          {loading ? (
            <Search size={16} className="animate-pulse" />
          ) : (
            <ChevronsUpDown size={16} />
          )}
        </div>
        {open && query.length >= MIN_QUERY_LEN && (
          <ul
            id={listboxId}
            role="listbox"
            className="absolute z-20 mt-1 max-h-72 w-full overflow-auto rounded-md border border-border bg-popover p-1 shadow-md"
          >
            {loading && results.length === 0 && (
              <li className="px-2 py-1.5 text-sm text-muted-foreground">Searching…</li>
            )}
            {!loading && results.length === 0 && (
              <li className="px-2 py-1.5 text-sm text-muted-foreground">
                No airports match.
              </li>
            )}
            {results.map((r, i) => (
              <li
                id={`${listboxId}-opt-${r.iata}`}
                key={r.iata}
                role="option"
                aria-selected={i === highlightIndex}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => handleSelect(r)}
                className={cn(
                  'flex cursor-pointer items-center justify-between rounded-sm px-2 py-1.5 text-sm',
                  i === highlightIndex ? 'bg-accent text-accent-foreground' : '',
                )}
              >
                {/* Single text node so RTL getByText can match the full
                    "IATA — Name" string (UI-SPEC copy). The city/country
                    subtext lives in a sibling element. */}
                <span>{`${r.iata} — ${r.name}`}</span>
                <span className="ms-3 text-xs text-muted-foreground">
                  {r.city}, {r.country}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

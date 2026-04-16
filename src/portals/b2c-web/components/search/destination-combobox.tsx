'use client';

// Hotel destination combobox (HOTB-01).
//
// Analog of `airport-combobox.tsx` per 04-PATTERNS §airport-combobox —
// cmdk-style combobox with a 200ms debounce, AbortController cancellation,
// and minimum 2-character query length (T-04-03-07 DoS mitigation against
// the destination typeahead). Min-length also matches the upstream
// rate-limiter pattern used by `/api/airports`.
//
// Label rendered per result row is "{City}, {Country}" — hotels index by
// city code so we store `cityCode` in value and surface the user-facing
// label in the input.
//
// Endpoint: `/api/hotels/destinations?q={q}&limit=10` (pass-through to
// Phase-2 `/hotels/destinations`; public and rate-limited same as
// /airports per 04-PLAN 04-03 T-04-03-07).

import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { Building2, ChevronsUpDown, Search } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface DestinationOption {
  cityCode: string;
  city: string;
  region?: string;
  country: string;
}

export interface DestinationComboboxProps {
  label: string;
  value: DestinationOption | null;
  onChange: (next: DestinationOption | null) => void;
  placeholder?: string;
  className?: string;
  disabled?: boolean;
}

const MIN_QUERY_LEN = 2;
const DEBOUNCE_MS = 200;

export function DestinationCombobox({
  label,
  value,
  onChange,
  placeholder = 'City, region or property',
  className,
  disabled,
}: DestinationComboboxProps) {
  const inputId = useId();
  const listboxId = useId();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<DestinationOption[]>([]);
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
      abortRef.current?.abort();
      setResults([]);
      setLoading(false);
      return;
    }

    debounceRef.current = setTimeout(() => {
      abortRef.current?.abort();
      const controller = new AbortController();
      abortRef.current = controller;
      setLoading(true);
      fetch(
        `/api/hotels/destinations?q=${encodeURIComponent(query)}&limit=10`,
        { signal: controller.signal },
      )
        .then(async (r) => {
          if (!r.ok) return [] as DestinationOption[];
          return (await r.json()) as DestinationOption[];
        })
        .then((data) => {
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

  useEffect(() => () => abortRef.current?.abort(), []);

  const selectedLabel = useMemo(() => {
    if (!value) return '';
    return `${value.city}, ${value.country}`;
  }, [value]);

  function handleSelect(opt: DestinationOption) {
    onChange(opt);
    setQuery(`${opt.city}, ${opt.country}`);
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
          <Building2 size={16} className="text-muted-foreground" />
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
              ? `${listboxId}-opt-${results[highlightIndex].cityCode}`
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
                No destinations match.
              </li>
            )}
            {results.map((r, i) => (
              <li
                id={`${listboxId}-opt-${r.cityCode}`}
                key={r.cityCode}
                role="option"
                aria-selected={i === highlightIndex}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => handleSelect(r)}
                className={cn(
                  'flex cursor-pointer items-center justify-between rounded-sm px-2 py-1.5 text-sm',
                  i === highlightIndex ? 'bg-accent text-accent-foreground' : '',
                )}
              >
                <span>{`${r.city}, ${r.country}`}</span>
                {r.region && (
                  <span className="ms-3 text-xs text-muted-foreground">{r.region}</span>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

// Plan 05-02 Task 3 — FlightSearchForm.
//
// Byte-mirrors the Phase 4 B2C search form: same fields (From / To / Depart /
// Return / Cabin / ADT / CHD / INF), same layout. Intentionally does NOT add
// agent-specific fields — the agency_id is stamped server-side (D-33 / T-05-02-01)
// and agent role is already in the JWT (Plan 01 session claim projection).
'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

export interface FlightSearchFormProps {
  defaultFrom?: string;
  defaultTo?: string;
  defaultDepart?: string;
  defaultReturn?: string;
  defaultCabin?: 'economy' | 'premium' | 'business' | 'first';
  defaultAdt?: number;
  defaultChd?: number;
  defaultInf?: number;
}

export function FlightSearchForm({
  defaultFrom = '',
  defaultTo = '',
  defaultDepart = '',
  defaultReturn = '',
  defaultCabin = 'economy',
  defaultAdt = 1,
  defaultChd = 0,
  defaultInf = 0,
}: FlightSearchFormProps) {
  const router = useRouter();
  const [from, setFrom] = useState(defaultFrom);
  const [to, setTo] = useState(defaultTo);
  const [depart, setDepart] = useState(defaultDepart);
  const [ret, setRet] = useState(defaultReturn);
  const [cabin, setCabin] = useState(defaultCabin);
  const [adt, setAdt] = useState(defaultAdt);
  const [chd, setChd] = useState(defaultChd);
  const [inf, setInf] = useState(defaultInf);

  function handleSubmit(evt: React.FormEvent) {
    evt.preventDefault();
    const params = new URLSearchParams({
      from,
      to,
      depart,
      ...(ret ? { return: ret } : {}),
      cabin,
      adt: String(adt),
      chd: String(chd),
      inf: String(inf),
    });
    router.push(`/search/flights?${params.toString()}`);
  }

  return (
    <form
      onSubmit={handleSubmit}
      aria-label="Flight search"
      className="grid gap-3 rounded-lg border border-zinc-200 bg-background p-4 md:grid-cols-5"
    >
      <label className="flex flex-col gap-1 text-sm">
        <span>From</span>
        <input
          required
          type="text"
          maxLength={3}
          value={from}
          onChange={(e) => setFrom(e.target.value.toUpperCase())}
          className="h-9 rounded-md border border-zinc-300 px-2 text-sm uppercase"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span>To</span>
        <input
          required
          type="text"
          maxLength={3}
          value={to}
          onChange={(e) => setTo(e.target.value.toUpperCase())}
          className="h-9 rounded-md border border-zinc-300 px-2 text-sm uppercase"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span>Depart</span>
        <input
          required
          type="date"
          value={depart}
          onChange={(e) => setDepart(e.target.value)}
          className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span>Return</span>
        <input
          type="date"
          value={ret}
          onChange={(e) => setRet(e.target.value)}
          className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span>Cabin</span>
        <select
          value={cabin}
          onChange={(e) => setCabin(e.target.value as FlightSearchFormProps['defaultCabin'] & string)}
          className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
        >
          <option value="economy">Economy</option>
          <option value="premium">Premium</option>
          <option value="business">Business</option>
          <option value="first">First</option>
        </select>
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span>Adults</span>
        <input
          type="number"
          min={1}
          max={9}
          value={adt}
          onChange={(e) => setAdt(Number(e.target.value))}
          className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span>Children</span>
        <input
          type="number"
          min={0}
          max={9}
          value={chd}
          onChange={(e) => setChd(Number(e.target.value))}
          className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span>Infants</span>
        <input
          type="number"
          min={0}
          max={4}
          value={inf}
          onChange={(e) => setInf(Number(e.target.value))}
          className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
        />
      </label>
      <button
        type="submit"
        className="col-span-full h-10 rounded-md bg-indigo-600 text-sm font-medium text-white hover:bg-indigo-700 md:col-span-1"
      >
        Search flights
      </button>
    </form>
  );
}

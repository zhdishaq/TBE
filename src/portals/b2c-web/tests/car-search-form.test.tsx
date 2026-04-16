// CarSearchForm unit tests — Plan 04-04 / Task 3a.
//
// Mirrors `hotel-search-form.test.tsx`. The form's validator is exposed as
// a pure function (`validateCarSearch`) so we can assert the zod-
// equivalent rules without mounting RHF / the entire component tree.

import { describe, it, expect } from 'vitest';
import { validateCarSearch } from '@/components/search/car-search-form';

describe('validateCarSearch', () => {
  const goodRange = {
    from: new Date('2099-05-01T00:00:00Z'),
    to: new Date('2099-05-04T00:00:00Z'),
  };

  it('accepts a fully-valid payload', () => {
    const errs = validateCarSearch({
      pickupLocation: 'LHR Terminal 5',
      range: goodRange,
      driverAge: 30,
    });
    expect(errs).toEqual([]);
  });

  it('rejects a missing pickup location', () => {
    const errs = validateCarSearch({
      pickupLocation: '',
      range: goodRange,
      driverAge: 30,
    });
    expect(errs.map((e) => e.field)).toContain('pickup');
  });

  it('rejects a pickup in the past', () => {
    const errs = validateCarSearch({
      pickupLocation: 'LHR',
      range: { from: new Date('2000-01-01T00:00:00Z'), to: new Date('2000-01-04T00:00:00Z') },
      driverAge: 30,
    });
    expect(errs.map((e) => e.field)).toContain('dates');
  });

  it('rejects a drop-off that is not after pickup', () => {
    const errs = validateCarSearch({
      pickupLocation: 'LHR',
      range: { from: goodRange.from, to: goodRange.from },
      driverAge: 30,
    });
    expect(errs.map((e) => e.field)).toContain('dates');
  });

  it('rejects a driver age < 18', () => {
    const errs = validateCarSearch({
      pickupLocation: 'LHR',
      range: goodRange,
      driverAge: 17,
    });
    expect(errs.map((e) => e.field)).toContain('driverAge');
  });

  it('rejects a driver age > 99', () => {
    const errs = validateCarSearch({
      pickupLocation: 'LHR',
      range: goodRange,
      driverAge: 100,
    });
    expect(errs.map((e) => e.field)).toContain('driverAge');
  });
});

// checkout-ref unit tests — Plan 04-04 / Task 3b <behavior>.
//
// parseCheckoutRef must accept the four canonical kinds (flight, hotel,
// basket, car), reject garbage, and be Array/null-safe (Next.js
// searchParams return `string | string[] | undefined`).

import { describe, it, expect } from 'vitest';
import {
  buildCheckoutRef,
  parseCheckoutRef,
} from '@/lib/checkout-ref';

describe('parseCheckoutRef', () => {
  it('parses flight refs', () => {
    expect(parseCheckoutRef('flight-abc123')).toEqual({
      kind: 'flight',
      id: 'abc123',
    });
  });

  it('parses hotel refs', () => {
    expect(parseCheckoutRef('hotel-XYZ_9-99')).toEqual({
      kind: 'hotel',
      id: 'XYZ_9-99',
    });
  });

  it('parses basket refs', () => {
    expect(parseCheckoutRef('basket-123')).toEqual({
      kind: 'basket',
      id: '123',
    });
  });

  it('parses car refs', () => {
    expect(parseCheckoutRef('car-CO-1')).toEqual({
      kind: 'car',
      id: 'CO-1',
    });
  });

  it('rejects unknown kinds', () => {
    expect(parseCheckoutRef('bogus-123')).toBeNull();
    expect(parseCheckoutRef('train-xyz')).toBeNull();
  });

  it('rejects malformed strings', () => {
    expect(parseCheckoutRef('')).toBeNull();
    expect(parseCheckoutRef('basket-')).toBeNull();
    expect(parseCheckoutRef('basket')).toBeNull();
    expect(parseCheckoutRef('-abc')).toBeNull();
  });

  it('rejects null, undefined, and array inputs (Next.js searchParams)', () => {
    expect(parseCheckoutRef(null)).toBeNull();
    expect(parseCheckoutRef(undefined)).toBeNull();
    expect(parseCheckoutRef(['basket-1', 'basket-2'])).toBeNull();
  });
});

describe('buildCheckoutRef', () => {
  it('round-trips with parseCheckoutRef', () => {
    const cases = [
      { kind: 'flight' as const, id: 'abc' },
      { kind: 'hotel' as const, id: 'XY_Z-1' },
      { kind: 'basket' as const, id: '123' },
      { kind: 'car' as const, id: 'CO-1' },
    ];
    for (const c of cases) {
      const raw = buildCheckoutRef(c.kind, c.id);
      expect(parseCheckoutRef(raw)).toEqual(c);
    }
  });
});

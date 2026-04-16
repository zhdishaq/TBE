// Trip Builder basket Zustand store (Plan 04-04 / PKG-01..04 / D-07/D-08).
//
// Persists to sessionStorage so a reload doesn't lose the in-progress
// trip. Only OFFER IDs and human-readable line-item metadata live
// client-side; authoritative pricing is server-computed at
// POST /baskets time (T-04-04-01).
//
// D-08: a single `clientSecret` + `paymentIntentId` pair per basket.
// Two-PI shapes (flightClientSecret + hotelClientSecret) are forbidden.

'use client';

import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import type {
  Basket,
  BasketStatus,
  CarLineItem,
  FlightLineItem,
  HotelLineItem,
} from '@/types/basket';

interface BasketActions {
  addFlight: (item: FlightLineItem) => void;
  addHotel: (item: HotelLineItem) => void;
  addCar: (item: CarLineItem) => void;
  removeFlight: () => void;
  removeHotel: () => void;
  removeCar: () => void;
  clear: () => void;
  /**
   * Create the server-side basket (POST /api/baskets). Resolves the
   * gateway's canonical BasketDtoPublic and stores the returned
   * `basketId`. Throws on HTTP error so the caller can surface a toast.
   */
  createServerBasket: (guest: {
    fullName: string;
    email: string;
    phoneNumber?: string;
  }) => Promise<string>;
  /**
   * Initialise the SINGLE combined PaymentIntent (D-08). Must be called
   * after createServerBasket. Stores the returned `clientSecret`.
   */
  initPaymentIntent: () => Promise<string>;
}

interface BasketState extends Basket {
  isSubmitting: boolean;
  error: string | null;
}

type Store = BasketState & BasketActions;

function emptyState(): BasketState {
  return {
    basketId: null,
    flight: undefined,
    hotel: undefined,
    car: undefined,
    totalAmount: 0,
    currency: 'GBP',
    clientSecret: null,
    paymentIntentId: null,
    status: 'Pending' satisfies BasketStatus,
    isSubmitting: false,
    error: null,
  };
}

/**
 * Recompute totalAmount + currency from whichever line items are present.
 * Currency falls back to the first populated item's currency, then GBP.
 */
function recalc(partial: Partial<BasketState>): Partial<BasketState> {
  const flight = partial.flight;
  const hotel = partial.hotel;
  const car = partial.car;
  const items = [flight, hotel, car].filter(
    (x): x is FlightLineItem | HotelLineItem | CarLineItem => Boolean(x),
  );
  const total = items.reduce((a, b) => a + b.amount.amount, 0);
  const currency = items[0]?.amount.currency ?? 'GBP';
  return { totalAmount: total, currency };
}

export const useBasket = create<Store>()(
  persist(
    (set, get) => ({
      ...emptyState(),

      addFlight: (item) =>
        set((s) => {
          const next = { ...s, flight: item };
          return { ...next, ...recalc(next) };
        }),
      addHotel: (item) =>
        set((s) => {
          const next = { ...s, hotel: item };
          return { ...next, ...recalc(next) };
        }),
      addCar: (item) =>
        set((s) => {
          const next = { ...s, car: item };
          return { ...next, ...recalc(next) };
        }),
      removeFlight: () =>
        set((s) => {
          const next = { ...s, flight: undefined };
          return { ...next, ...recalc(next) };
        }),
      removeHotel: () =>
        set((s) => {
          const next = { ...s, hotel: undefined };
          return { ...next, ...recalc(next) };
        }),
      removeCar: () =>
        set((s) => {
          const next = { ...s, car: undefined };
          return { ...next, ...recalc(next) };
        }),
      clear: () => set(() => emptyState()),

      createServerBasket: async (guest) => {
        const s = get();
        if (!s.flight || !s.hotel) {
          throw new Error('Basket must contain both a flight and a hotel before checkout.');
        }
        set({ isSubmitting: true, error: null });
        try {
          const body = JSON.stringify({
            flightOfferId: s.flight.offerId,
            hotelOfferId: s.hotel.offerId,
            carOfferId: s.car?.offerId ?? null,
            currency: s.currency,
            flightSubtotalHint: s.flight.amount.amount,
            hotelSubtotalHint: s.hotel.amount.amount,
            guest,
          });
          const resp = await fetch('/api/baskets', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body,
          });
          if (!resp.ok) throw new Error(`Basket create failed: ${resp.status}`);
          const dto = (await resp.json()) as {
            basketId?: string;
            status?: BasketStatus;
          };
          if (!dto.basketId) throw new Error('Basket response missing basketId');
          set({
            basketId: dto.basketId,
            status: dto.status ?? 'Pending',
            isSubmitting: false,
          });
          return dto.basketId;
        } catch (err) {
          set({
            isSubmitting: false,
            error: err instanceof Error ? err.message : 'Basket create failed.',
          });
          throw err;
        }
      },

      initPaymentIntent: async () => {
        const s = get();
        if (!s.basketId) {
          throw new Error('basketId is required — call createServerBasket first.');
        }
        set({ isSubmitting: true, error: null });
        try {
          const resp = await fetch(
            `/api/baskets/${encodeURIComponent(s.basketId)}/payment-intents`,
            { method: 'POST' },
          );
          if (!resp.ok) throw new Error(`PaymentIntent init failed: ${resp.status}`);
          const dto = (await resp.json()) as { clientSecret?: string };
          if (!dto.clientSecret) throw new Error('PaymentIntent response missing clientSecret');
          set({ clientSecret: dto.clientSecret, isSubmitting: false });
          return dto.clientSecret;
        } catch (err) {
          set({
            isSubmitting: false,
            error: err instanceof Error ? err.message : 'PaymentIntent init failed.',
          });
          throw err;
        }
      },
    }),
    {
      name: 'tbe-basket',
      // sessionStorage: survives reloads within the tab but NOT across
      // closes — a basket abandoned overnight should not silently come
      // back with stale pricing.
      storage: createJSONStorage(() =>
        typeof window !== 'undefined' ? window.sessionStorage : (undefined as unknown as Storage),
      ),
      // Persist only the serialisable line items + ids; transient UI
      // flags (isSubmitting, error) stay in memory.
      partialize: (s) => ({
        basketId: s.basketId,
        flight: s.flight,
        hotel: s.hotel,
        car: s.car,
        totalAmount: s.totalAmount,
        currency: s.currency,
        clientSecret: s.clientSecret,
        paymentIntentId: s.paymentIntentId,
        status: s.status,
      }),
    },
  ),
);

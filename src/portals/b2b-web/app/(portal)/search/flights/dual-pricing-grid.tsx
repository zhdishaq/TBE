// Plan 05-02 Task 3 RED stub -- DualPricingGrid.
// Intentionally minimal so the TDD RED gate sees a failing render contract.
'use client';

export interface PricedOffer {
  offerId: string;
  airline: string;
  flightNumber: string;
  departAt: string;
  arriveAt: string;
  durationMinutes: number;
  stops: number;
  net: number;
  markup: number;
  gross: number;
  commission: number;
  currency: string;
}

export type SortKey = 'gross-asc';

export interface DualPricingGridProps {
  offers: PricedOffer[];
  initialSort?: SortKey;
  initialSelected?: string;
  onSelect?: (offerId: string) => void;
}

export function DualPricingGrid(_props: DualPricingGridProps) {
  return <div>dual pricing grid placeholder</div>;
}

'use client';

// D-09 partial-failure banner (Plan 04-04 / PKG-01..04).
//
// Copy is the UI-SPEC single-statement-entry language: the customer sees
// exactly ONE charge on their card statement even when the hotel leg
// failed. The combined-payment-form uses the same "ONE charge" phrasing
// on the pay-button, so a customer who reads only the banner OR only
// the form never gets conflicting messages.
//
// Acceptance grep: "ONE charge" + "only the flight portion was charged"
// must both appear literally.

import { AlertTriangle } from 'lucide-react';

export interface PartialFailureBannerProps {
  flightReference: string;
  onFindAnotherHotel?: () => void;
  className?: string;
}

export function PartialFailureBanner({
  flightReference,
  onFindAnotherHotel,
  className,
}: PartialFailureBannerProps) {
  return (
    <div
      role="alert"
      className={[
        'flex flex-col gap-3 rounded-md border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900',
        className,
      ].filter(Boolean).join(' ')}
    >
      <div className="flex items-start gap-2">
        <AlertTriangle size={18} className="mt-0.5 shrink-0 text-amber-600" />
        <div className="flex flex-col gap-2">
          <p className="font-medium">
            We&apos;ve confirmed your flight (ref {flightReference}).
          </p>
          <p>
            The hotel became unavailable during payment, so only the flight
            portion was charged — you&apos;ll see ONE charge on your statement.
            Try another hotel?
          </p>
        </div>
      </div>
      <div className="flex justify-end">
        <button
          type="button"
          onClick={onFindAnotherHotel}
          className="inline-flex items-center gap-1.5 rounded-md bg-amber-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-amber-700"
        >
          Find another hotel
        </button>
      </div>
    </div>
  );
}

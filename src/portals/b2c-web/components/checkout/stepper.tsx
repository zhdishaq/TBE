// Checkout stepper — 5-step mobile/desktop indicator (B2C-05).
//
// Step sequence (per UI-SPEC):
//   1. Search    — /
//   2. Results   — /flights/results
//   3. Select    — /flights/<offerId>
//   4. Details   — /checkout/details
//   5. Payment   — /checkout/payment
//
// Success + Processing are NOT shown as steps — they are the outcome
// after payment, not user-driven screens (B2C-05).

'use client';

import { cn } from '@/lib/utils';

export type CheckoutStep = 'search' | 'results' | 'select' | 'details' | 'payment';

const STEPS: readonly { key: CheckoutStep; label: string }[] = [
  { key: 'search', label: 'Search' },
  { key: 'results', label: 'Results' },
  { key: 'select', label: 'Select' },
  { key: 'details', label: 'Details' },
  { key: 'payment', label: 'Payment' },
];

interface StepperProps {
  currentStep: CheckoutStep;
  className?: string;
}

export function CheckoutStepper({ currentStep, className }: StepperProps) {
  const currentIdx = STEPS.findIndex((s) => s.key === currentStep);

  return (
    <nav
      aria-label="Checkout progress"
      className={cn('mx-auto flex w-full max-w-3xl items-center justify-between px-6 py-4', className)}
    >
      <ol className="flex w-full items-center">
        {STEPS.map((step, i) => {
          const isPast = i < currentIdx;
          const isCurrent = i === currentIdx;
          return (
            <li key={step.key} className="flex flex-1 items-center last:flex-none">
              <div className="flex flex-col items-center">
                <span
                  aria-current={isCurrent ? 'step' : undefined}
                  className={cn(
                    'flex h-8 w-8 items-center justify-center rounded-full border text-xs font-semibold',
                    isPast && 'border-blue-600 bg-blue-600 text-white',
                    isCurrent && 'border-blue-600 text-blue-600',
                    !isPast && !isCurrent && 'border-muted text-muted-foreground',
                  )}
                >
                  {i + 1}
                </span>
                <span
                  className={cn(
                    'mt-1 hidden text-xs md:block',
                    (isPast || isCurrent) ? 'text-foreground' : 'text-muted-foreground',
                  )}
                >
                  {step.label}
                </span>
              </div>
              {i < STEPS.length - 1 && (
                <span
                  className={cn(
                    'mx-2 h-px flex-1',
                    i < currentIdx ? 'bg-blue-600' : 'bg-border',
                  )}
                  aria-hidden="true"
                />
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

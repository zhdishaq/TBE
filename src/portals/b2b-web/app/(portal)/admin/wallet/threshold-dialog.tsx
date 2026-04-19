// Plan 05-05 Task 4 — ThresholdDialog.
//
// Radix Dialog (NOT AlertDialog — this is non-destructive; D-44 reserves
// AlertDialog for void/release destructive flows only).
//
// Note: the project uses the `radix-ui` barrel package rather than split
// `@radix-ui/react-dialog` — both expose `Dialog.Root`/`Dialog.Trigger`/
// `Dialog.Content` etc. The `ui/dialog.jsx` shell component re-exports
// these named primitives; we consume them via that shell so the whole
// portal stays single-sourced on one Radix import.
//
// Pitfall 28 defence: the mutation body sends ONLY {thresholdAmount, currency} —
// NEVER the agencyId. Backend reads agency_id from the JWT claim only.

'use client';

import { useEffect, useState } from 'react';
import { useForm, FormProvider } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';

// D-40 parity: threshold range matches top-up cap policy (£50-£10,000).
const thresholdSchema = z.object({
  thresholdAmount: z.coerce
    .number({ message: 'Threshold is required' })
    .min(50, 'Threshold must be between £50 and £10 000')
    .max(10000, 'Threshold must be between £50 and £10 000'),
});
// z.coerce.number() has input=unknown, output=number. Use z.input for
// useForm's TFieldValues so the resolver's input type aligns; z.output
// is what handleSubmit receives after coercion.
type ThresholdFormInput = z.input<typeof thresholdSchema>;
type ThresholdFormValues = z.output<typeof thresholdSchema>;

interface ThresholdCacheEntry {
  threshold: number;
  currency: string;
}

export function ThresholdDialog(): React.ReactElement {
  const qc = useQueryClient();
  const cached =
    qc.getQueryData<ThresholdCacheEntry>(['wallet', 'threshold']) ?? {
      threshold: 500,
      currency: 'GBP',
    };

  const [open, setOpen] = useState(false);

  const methods = useForm<ThresholdFormInput, unknown, ThresholdFormValues>({
    resolver: zodResolver(thresholdSchema),
    defaultValues: { thresholdAmount: cached.threshold },
  });
  const { register, handleSubmit, reset, formState: { errors } } = methods;

  // Re-sync when the dialog opens (cache may have changed since mount).
  useEffect(() => {
    if (open) reset({ thresholdAmount: cached.threshold });
  }, [open, cached.threshold, reset]);

  const mutation = useMutation({
    mutationFn: async (amount: number) => {
      const resp = await fetch('/api/wallet/threshold', {
        method: 'PUT',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ thresholdAmount: amount, currency: cached.currency }),
      });
      if (!resp.ok) throw new Error(`threshold ${resp.status}`);
      return amount;
    },
    onSuccess: (amount) => {
      qc.setQueryData(['wallet', 'threshold'], { threshold: amount, currency: cached.currency });
      // Balance + transactions need to re-fetch so the banner re-evaluates
      // hysteresis and the ledger shows any recent top-ups.
      qc.invalidateQueries({ queryKey: ['wallet'] });
      setOpen(false);
    },
  });

  const onSubmit = handleSubmit(({ thresholdAmount }) => {
    mutation.mutate(thresholdAmount);
  });

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <button
          type="button"
          className="h-9 rounded-md border px-3 text-sm font-medium hover:bg-accent"
        >
          Edit threshold
        </button>
      </DialogTrigger>
      {/*
        Accessible-naming contract:
        - DialogTitle "Edit threshold" → Radix wires aria-labelledby so
          `getByRole('dialog', { name: /threshold/i })` matches.
        - <label>Threshold (£)</label> → bound via for=threshold-amount so
          `getByLabelText(/Threshold \(£\)/i)` uniquely matches the input
          (tighter regex avoids collision with the dialog's own label chain).
      */}
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit threshold</DialogTitle>
        </DialogHeader>
        <FormProvider {...methods}>
          <form onSubmit={onSubmit} className="space-y-4">
            <div className="space-y-1">
              <label htmlFor="threshold-amount" className="text-sm font-medium">
                Threshold (£)
              </label>
              <input
                id="threshold-amount"
                type="number"
                step="0.01"
                min={50}
                max={10000}
                {...register('thresholdAmount')}
                className="w-full rounded-md border px-3 py-2 text-sm tabular-nums"
                aria-invalid={errors.thresholdAmount ? 'true' : 'false'}
              />
              {errors.thresholdAmount && (
                <p role="alert" className="text-sm text-red-600">
                  {errors.thresholdAmount.message}
                </p>
              )}
            </div>
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="h-9 rounded-md border px-3 text-sm"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={mutation.isPending}
                className="h-9 rounded-md bg-indigo-600 px-3 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50"
              >
                {mutation.isPending ? 'Saving...' : 'Save'}
              </button>
            </div>
          </form>
        </FormProvider>
      </DialogContent>
    </Dialog>
  );
}

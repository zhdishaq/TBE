// Plan 05-02 Task 3 — CheckoutDetailsForm.
//
// Captures passenger list, customer contact (B2B-04), and (admin-only)
// AgencyMarkupOverride (D-37). Client-side gate is UX polish only — the
// server still enforces admin-only overrides in AgentBookingsController
// (Task 2). Uses react-hook-form + zod for schema validation.
'use client';

import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useFieldArray, useForm } from 'react-hook-form';
import { z } from 'zod';

const PassengerSchema = z.object({
  firstName: z.string().min(1).max(64),
  lastName: z.string().min(1).max(64),
  dob: z.string().min(4),
});

const Schema = z.object({
  passengers: z.array(PassengerSchema).min(1),
  customerName: z.string().min(1).max(200),
  customerEmail: z.string().email().max(320),
  customerPhone: z.string().min(5).max(32),
  agencyMarkupOverride: z
    .number()
    .min(0)
    .max(100000)
    .optional(),
});

type FormValues = z.infer<typeof Schema>;

export interface CheckoutDetailsFormProps {
  roles: string[];
  onSubmit?: (values: FormValues) => void | Promise<void>;
}

export function CheckoutDetailsForm({ roles, onSubmit }: CheckoutDetailsFormProps) {
  const isAdmin = roles.includes('agent-admin');
  const [submitting, setSubmitting] = useState(false);

  const form = useForm<FormValues>({
    resolver: zodResolver(Schema),
    defaultValues: {
      passengers: [{ firstName: '', lastName: '', dob: '' }],
      customerName: '',
      customerEmail: '',
      customerPhone: '',
      agencyMarkupOverride: undefined,
    },
  });

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: 'passengers',
  });

  async function handleSubmit(values: FormValues) {
    setSubmitting(true);
    try {
      await onSubmit?.(values);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form
      onSubmit={form.handleSubmit(handleSubmit)}
      aria-label="Checkout details"
      className="flex flex-col gap-6"
    >
      <fieldset className="flex flex-col gap-3 rounded-lg border border-zinc-200 p-4">
        <legend className="px-1 text-sm font-semibold">Passengers</legend>
        {fields.map((field, idx) => (
          <div key={field.id} className="grid grid-cols-3 gap-2">
            <input
              aria-label={`Passenger ${idx + 1} first name`}
              placeholder="First name"
              {...form.register(`passengers.${idx}.firstName`)}
              className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
            />
            <input
              aria-label={`Passenger ${idx + 1} last name`}
              placeholder="Last name"
              {...form.register(`passengers.${idx}.lastName`)}
              className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
            />
            <div className="flex gap-2">
              <input
                aria-label={`Passenger ${idx + 1} date of birth`}
                type="date"
                {...form.register(`passengers.${idx}.dob`)}
                className="h-9 flex-1 rounded-md border border-zinc-300 px-2 text-sm"
              />
              {fields.length > 1 && (
                <button
                  type="button"
                  onClick={() => remove(idx)}
                  className="h-9 rounded-md border border-zinc-300 px-2 text-xs"
                >
                  Remove
                </button>
              )}
            </div>
          </div>
        ))}
        <button
          type="button"
          onClick={() => append({ firstName: '', lastName: '', dob: '' })}
          className="h-9 self-start rounded-md border border-zinc-300 px-3 text-sm"
        >
          Add passenger
        </button>
      </fieldset>

      <fieldset className="flex flex-col gap-3 rounded-lg border border-zinc-200 p-4">
        <legend className="px-1 text-sm font-semibold">Customer contact</legend>
        <label className="flex flex-col gap-1 text-sm">
          <span>Full name</span>
          <input
            {...form.register('customerName')}
            className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span>Email</span>
          <input
            type="email"
            {...form.register('customerEmail')}
            className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
          />
          {form.formState.errors.customerEmail && (
            <span className="text-xs text-red-700">
              {form.formState.errors.customerEmail.message}
            </span>
          )}
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span>Phone</span>
          <input
            {...form.register('customerPhone')}
            className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
          />
        </label>
      </fieldset>

      {isAdmin && (
        <fieldset className="flex flex-col gap-3 rounded-lg border border-zinc-200 p-4">
          <legend className="px-1 text-sm font-semibold">
            Agency markup override (agent-admin only)
          </legend>
          <label className="flex flex-col gap-1 text-sm">
            <span>Override amount</span>
            <input
              type="number"
              step="0.01"
              min="0"
              max="100000"
              aria-label="Agency markup override"
              {...form.register('agencyMarkupOverride', { valueAsNumber: true })}
              className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
            />
          </label>
        </fieldset>
      )}

      <button
        type="submit"
        disabled={submitting}
        className="h-10 w-40 rounded-md bg-indigo-600 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
      >
        {submitting ? 'Saving…' : 'Continue'}
      </button>
    </form>
  );
}

// Plan 05-01 Task 2 — Create sub-agent dialog.
//
// Radix `Dialog` (creation is non-destructive — so NOT a destructive
// confirmation dialog). Form
// is react-hook-form + zod. CRITICAL: the body sent to /api/agents
// deliberately OMITS agency_id — Pitfall 28 requires that server-side
// injection be the single source of truth. Even if a dev later adds
// a hidden input for agency_id by mistake, the API's zod schema
// rejects unknown keys.
//
// UI-SPEC §Copywriting (verbatim):
//   primary CTA : `Create sub-agent`
//   success toast (parent): `Sub-agent created. They will receive a
//                             verification email at {email}.`
//   duplicate error (inline): `A user with this email already exists in
//                              your agency.`
//   unknown error (toast): `We couldn't create that sub-agent. Please
//                           try again. If the problem continues,
//                           contact support.`

'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { cn } from '@/lib/utils';

export interface CreateSubAgentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Parent toast hook: invoked with the newly-created sub-agent's
   *  email so the toast can render the verified copy from UI-SPEC. */
  onSuccess?: (email: string) => void;
}

const FormSchema = z.object({
  firstName: z.string().min(1, 'First name is required').max(100),
  lastName: z.string().min(1, 'Last name is required').max(100),
  email: z.string().email('Enter a valid email address'),
  // T-05-01-06: agent-admin is literally unavailable here.
  role: z.enum(['agent', 'agent-readonly'], {
    message: 'Select a role',
  }),
});

type FormValues = z.infer<typeof FormSchema>;

async function postCreate(values: FormValues): Promise<void> {
  const resp = await fetch('/api/agents', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    // Pitfall 28: `agency_id` is absent from the body on purpose.
    // Every field shipped here is in the server-side zod schema.
    body: JSON.stringify(values),
  });
  if (resp.status === 202) return;
  if (resp.status === 409) throw new Error('duplicate_email');
  if (resp.status === 400) throw new Error('invalid_body');
  throw new Error('create_failed');
}

export function CreateSubAgentDialog({
  open,
  onOpenChange,
  onSuccess,
}: CreateSubAgentDialogProps) {
  const queryClient = useQueryClient();
  const form = useForm<FormValues>({
    resolver: zodResolver(FormSchema),
    defaultValues: {
      firstName: '',
      lastName: '',
      email: '',
      role: 'agent',
    },
  });
  const mutation = useMutation({
    mutationFn: (values: FormValues) => postCreate(values),
    onSuccess: (_data, values) => {
      queryClient.invalidateQueries({ queryKey: ['agency-users'] });
      onSuccess?.(values.email);
      form.reset();
      onOpenChange(false);
    },
    onError: (err: Error) => {
      if (err.message === 'duplicate_email') {
        form.setError('email', {
          type: 'duplicate',
          message: 'A user with this email already exists in your agency.',
        });
      }
    },
  });

  const onSubmit = form.handleSubmit((values) => mutation.mutate(values));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Create sub-agent</DialogTitle>
          <DialogDescription>
            Invite a teammate under your agency. They&rsquo;ll receive a
            verification email before they can log in.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={onSubmit} className="flex flex-col gap-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="firstName">First name</Label>
              <Input
                id="firstName"
                autoComplete="given-name"
                aria-invalid={
                  form.formState.errors.firstName ? 'true' : undefined
                }
                {...form.register('firstName')}
              />
              {form.formState.errors.firstName ? (
                <p className="text-xs text-destructive">
                  {form.formState.errors.firstName.message}
                </p>
              ) : null}
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="lastName">Last name</Label>
              <Input
                id="lastName"
                autoComplete="family-name"
                aria-invalid={
                  form.formState.errors.lastName ? 'true' : undefined
                }
                {...form.register('lastName')}
              />
              {form.formState.errors.lastName ? (
                <p className="text-xs text-destructive">
                  {form.formState.errors.lastName.message}
                </p>
              ) : null}
            </div>
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              aria-invalid={form.formState.errors.email ? 'true' : undefined}
              {...form.register('email')}
            />
            {form.formState.errors.email ? (
              <p
                className={cn(
                  'text-xs',
                  form.formState.errors.email.type === 'duplicate'
                    ? 'text-destructive'
                    : 'text-destructive',
                )}
              >
                {form.formState.errors.email.message}
              </p>
            ) : null}
          </div>
          <fieldset className="flex flex-col gap-2">
            <legend className="text-sm font-medium text-foreground">
              Role
            </legend>
            <RadioGroup
              defaultValue="agent"
              onValueChange={(value: string) =>
                form.setValue('role', value as 'agent' | 'agent-readonly', {
                  shouldValidate: true,
                })
              }
              className="flex flex-col gap-2"
            >
              <div className="flex items-center gap-2">
                <RadioGroupItem id="role-agent" value="agent" />
                <Label htmlFor="role-agent" className="font-normal">
                  Agent — can search, book, and view bookings under the agency.
                </Label>
              </div>
              <div className="flex items-center gap-2">
                <RadioGroupItem id="role-ro" value="agent-readonly" />
                <Label htmlFor="role-ro" className="font-normal">
                  Agent (read-only) — can view bookings but cannot create new
                  ones.
                </Label>
              </div>
            </RadioGroup>
          </fieldset>
          {mutation.isError &&
          (mutation.error as Error).message !== 'duplicate_email' ? (
            <p role="alert" className="text-sm text-destructive">
              We couldn&rsquo;t create that sub-agent. Please try again. If
              the problem continues, contact support.
            </p>
          ) : null}
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
              disabled={mutation.isPending}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? 'Creating…' : 'Create sub-agent'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

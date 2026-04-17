// Plan 05-02 Task 3 RED stub -- DebitSummary.
'use client';

export interface DebitSummaryProps {
  gross: number;
  currency: string;
  balance: number;
  onConfirm: string;
  payload: Record<string, unknown>;
  roles: string[];
  adminEmail?: string;
}

export function DebitSummary(_props: DebitSummaryProps) {
  return <div>debit summary placeholder</div>;
}

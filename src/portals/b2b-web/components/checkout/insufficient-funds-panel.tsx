// Plan 05-02 Task 3 RED stub -- InsufficientFundsPanel.
'use client';

export interface InsufficientFundsPanelProps {
  gross: number;
  balance: number;
  currency: string;
  roles: string[];
  adminEmail?: string;
}

export function InsufficientFundsPanel(_props: InsufficientFundsPanelProps) {
  return <div>insufficient funds placeholder</div>;
}

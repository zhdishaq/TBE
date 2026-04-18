// Plan 05-05 Task 1 RED stub — WalletPaymentElementWrapper.
//
// Real body lands in the GREEN commit. Stub keeps imports compilable and
// deliberately returns null so the test that asserts <Elements> mount fails.
'use client';

import type { ReactNode } from 'react';

export interface WalletPaymentElementWrapperProps {
  clientSecret: string | null;
  children?: ReactNode;
}

export function WalletPaymentElementWrapper(
  _props: WalletPaymentElementWrapperProps,
): React.ReactElement | null {
  return null;
}

// Plan 05-01 Task 1 — client island for the Sign-in CTA.
//
// The RSC page can't call signIn() directly because signIn is a client
// helper from next-auth/react. We split the button into this client
// component so the RSC page stays server-rendered and can still redirect
// authenticated visitors at render time.

'use client';

import { signIn } from 'next-auth/react';
import { Button } from '@/components/ui/button';

interface SignInButtonProps {
  callbackUrl: string;
}

export function SignInButton({ callbackUrl }: SignInButtonProps) {
  return (
    <Button
      type="button"
      onClick={() => signIn('keycloak', { callbackUrl })}
      className="w-full"
    >
      Sign in
    </Button>
  );
}

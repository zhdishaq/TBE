// Password-reset handoff (D-04). We never prompt for an email address
// here — we bounce the user straight to Keycloak's hosted
// `/login-actions/reset-credentials` flow, which handles the whole
// cycle (email lookup → magic link → new password). This matches how
// `/register` and `/login` work.
//
// Server Component — no client JS.

import { redirect } from 'next/navigation';

export const dynamic = 'force-dynamic';

export default function PasswordResetPage() {
  const issuer = requireEnv('KEYCLOAK_B2C_ISSUER').replace(/\/+$/, '');
  const clientId = requireEnv('KEYCLOAK_B2C_CLIENT_ID');

  const params = new URLSearchParams({ client_id: clientId });
  redirect(`${issuer}/login-actions/reset-credentials?${params.toString()}`);
}

function requireEnv(name: string): string {
  const v = process.env[name];
  if (!v) throw new Error(`${name} is not set`);
  return v;
}

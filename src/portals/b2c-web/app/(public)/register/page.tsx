// B2C register route — redirects straight to Keycloak's hosted
// registration page (D-04: Keycloak owns all identity surfaces; we never
// reimplement registration / login / password-reset / email-verify).
//
// This is a Server Component — no client JS. The `redirect()` happens
// during the request so search crawlers and direct-link visitors land
// on Keycloak's page immediately.

import { redirect } from 'next/navigation';

export const dynamic = 'force-dynamic';

export default function RegisterPage() {
  const issuer = requireEnv('KEYCLOAK_B2C_ISSUER').replace(/\/+$/, '');
  const clientId = requireEnv('KEYCLOAK_B2C_CLIENT_ID');
  const nextAuthUrl = (process.env.NEXTAUTH_URL ?? '').replace(/\/+$/, '');

  const redirectUri = `${nextAuthUrl}/api/auth/callback/keycloak`;
  const params = new URLSearchParams({
    client_id: clientId,
    response_type: 'code',
    scope: 'openid email profile',
    redirect_uri: redirectUri,
  });

  redirect(
    `${issuer}/protocol/openid-connect/registrations?${params.toString()}`,
  );
}

function requireEnv(name: string): string {
  const v = process.env[name];
  if (!v) throw new Error(`${name} is not set`);
  return v;
}

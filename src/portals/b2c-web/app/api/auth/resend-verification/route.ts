// Resend-verification route handler.
//
// Pitfall 8: the user's own B2C access token is NOT sufficient to call
// Keycloak's `send-verify-email` admin endpoint. We mint a fresh
// service-account token via `tbe-b2c-admin` (`lib/keycloak-admin.ts`)
// and call the admin API server-side.
//
// Runtime: Node only. Never Edge — the Keycloak admin client logic uses
// Node `fetch` + in-process caching that is not guaranteed under Edge's
// per-request worker model.

import { auth } from '@/lib/auth';
import { sendVerifyEmail } from '@/lib/keycloak-admin';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST() {
  const session = await auth();
  if (!session?.user?.id) {
    return new Response(null, { status: 401 });
  }

  try {
    await sendVerifyEmail(session.user.id);
  } catch {
    // Never echo Keycloak internals to the caller. The backend logs
    // already capture enough context for ops.
    return new Response(null, { status: 502 });
  }

  return new Response(null, { status: 202 });
}

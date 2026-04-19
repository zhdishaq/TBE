// Server-side gateway fetch helper (CONTEXT D-05).
//
// Forwards the Keycloak access token as an `Authorization: Bearer …`
// header so YARP can validate it and route to the right backend. Runs
// ONLY in server components or route handlers — never imported from a
// "use client" file (access_token must never leak to the browser).
//
// Source: 04-RESEARCH Pattern 1.

import { auth } from '@/lib/auth';

export class UnauthenticatedError extends Error {
  constructor() {
    super('Unauthenticated');
    this.name = 'UnauthenticatedError';
  }
}

export async function gatewayFetch(
  path: string,
  init?: RequestInit,
): Promise<Response> {
  const session = await auth();
  if (!session?.access_token) {
    throw new UnauthenticatedError();
  }

  const gatewayUrl = process.env.GATEWAY_URL;
  if (!gatewayUrl) {
    throw new Error('GATEWAY_URL environment variable not set');
  }

  const headers = new Headers(init?.headers);
  headers.set('Authorization', `Bearer ${session.access_token}`);
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  return fetch(`${gatewayUrl}${path}`, {
    ...init,
    headers,
    cache: 'no-store',
  });
}

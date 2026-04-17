// Plan 05-02 Task 3 RED test -- /api/wallet/balance route.
//
// Contract:
//   - No session OR session without agency_id -> 403 (D-33 + Pitfall 26 --
//     never leak cross-tenant data even with a valid access token).
//   - Valid session -> gatewayFetch('/api/b2b/wallet/balance') is invoked
//     and the response body is forwarded unchanged.
//   - gateway 5xx / throw -> 502.
//   - `runtime = 'nodejs'` so `auth()` (NextAuth v5) can run.

import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/auth', () => ({ auth: vi.fn() }));
vi.mock('@/lib/api-client', async () => {
  const actual = await vi.importActual<typeof import('@/lib/api-client')>('@/lib/api-client');
  return {
    ...actual,
    gatewayFetch: vi.fn(),
  };
});

import * as authMod from '@/lib/auth';
import * as apiClient from '@/lib/api-client';

async function importRoute() {
  return await import('@/app/api/wallet/balance/route');
}

describe('/api/wallet/balance route handler', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.mocked(authMod.auth).mockReset();
    vi.mocked(apiClient.gatewayFetch).mockReset();
  });

  it('declares nodejs runtime so auth() can execute', async () => {
    const mod = await importRoute();
    expect(mod.runtime).toBe('nodejs');
  });

  it('returns 403 when the session has no agency_id claim', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { name: 'Ada' },
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    const mod = await importRoute();
    const res = await mod.GET();
    expect(res.status).toBe(403);
  });

  it('returns 403 when there is no session at all', async () => {
    vi.mocked(authMod.auth).mockResolvedValue(null);
    const mod = await importRoute();
    const res = await mod.GET();
    expect(res.status).toBe(403);
  });

  it('forwards the gateway JSON response when the session is valid', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { name: 'Ada', agency_id: 'AG-001' },
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    vi.mocked(apiClient.gatewayFetch).mockResolvedValue(
      new Response(JSON.stringify({ amount: 1234, currency: 'GBP' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    const mod = await importRoute();
    const res = await mod.GET();
    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({ amount: 1234, currency: 'GBP' });
    expect(apiClient.gatewayFetch).toHaveBeenCalledWith('/api/b2b/wallet/balance');
  });
});

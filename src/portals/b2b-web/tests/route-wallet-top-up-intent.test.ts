// Plan 05-05 Task 2 — /api/wallet/top-up/intent route handler.
//
// Contract:
//   - No session OR session without agency_id → 403 (Pitfall 28 / D-33).
//   - Session lacks agent-admin role → 403 (B2BAdminPolicy parity).
//   - Happy path: Bearer forwarded via gatewayFetch; body is JSON-forwarded
//     with any client-supplied `agencyId` stripped (defence-in-depth).
//   - Upstream 400 application/problem+json is streamed through with the
//     Content-Type header preserved verbatim.
//   - runtime = 'nodejs' so auth() can decrypt the session cookie.

import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/auth', () => ({ auth: vi.fn() }));
vi.mock('@/lib/api-client', async () => {
  const actual = await vi.importActual<typeof import('@/lib/api-client')>(
    '@/lib/api-client',
  );
  return {
    ...actual,
    gatewayFetch: vi.fn(),
  };
});

import * as authMod from '@/lib/auth';
import * as apiClient from '@/lib/api-client';

async function importRoute() {
  return await import('@/app/api/wallet/top-up/intent/route');
}

function makePostRequest(body: unknown): Request {
  return new Request('http://localhost/api/wallet/top-up/intent', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
}

describe('/api/wallet/top-up/intent route handler', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.mocked(authMod.auth).mockReset();
    vi.mocked(apiClient.gatewayFetch).mockReset();
  });

  it('declares nodejs runtime', async () => {
    const mod = await importRoute();
    expect(mod.runtime).toBe('nodejs');
  });

  it('returns 403 when the session has no agency_id', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { name: 'Ada' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    const mod = await importRoute();
    const res = await mod.POST(makePostRequest({ amount: 100 }));
    expect(res.status).toBe(403);
    expect(apiClient.gatewayFetch).not.toHaveBeenCalled();
  });

  it('returns 403 when the session lacks agent-admin role', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { name: 'Ada', agency_id: 'AG-001' },
      roles: ['agent'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    const mod = await importRoute();
    const res = await mod.POST(makePostRequest({ amount: 100 }));
    expect(res.status).toBe(403);
    expect(apiClient.gatewayFetch).not.toHaveBeenCalled();
  });

  it('forwards the gateway happy-path response verbatim', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { name: 'Ada', agency_id: 'AG-001' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    const upstream = new Response(
      JSON.stringify({ clientSecret: 'pi_123_secret_abc' }),
      { status: 200, headers: { 'content-type': 'application/json' } },
    );
    vi.mocked(apiClient.gatewayFetch).mockResolvedValue(upstream);

    const mod = await importRoute();
    const res = await mod.POST(makePostRequest({ amount: 250 }));

    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({ clientSecret: 'pi_123_secret_abc' });
    expect(apiClient.gatewayFetch).toHaveBeenCalledTimes(1);
    const [path, init] = vi.mocked(apiClient.gatewayFetch).mock.calls[0];
    expect(path).toContain('/wallet/top-up/intent');
    expect(init?.method).toBe('POST');
  });

  it('streams problem+json Content-Type verbatim on a 400 response', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { name: 'Ada', agency_id: 'AG-001' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    const upstream = new Response(
      JSON.stringify({
        type: '/errors/wallet-topup-out-of-range',
        allowedRange: { min: 10, max: 50000, currency: 'GBP' },
        requested: 5,
      }),
      { status: 400, headers: { 'content-type': 'application/problem+json' } },
    );
    vi.mocked(apiClient.gatewayFetch).mockResolvedValue(upstream);

    const mod = await importRoute();
    const res = await mod.POST(makePostRequest({ amount: 5 }));

    expect(res.status).toBe(400);
    expect(res.headers.get('content-type')).toBe('application/problem+json');
    const body = (await res.json()) as { type: string };
    expect(body.type).toBe('/errors/wallet-topup-out-of-range');
  });

  it('strips body-supplied agencyId before forwarding (Pitfall 28 defence-in-depth)', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { name: 'Ada', agency_id: 'AG-jwt' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    vi.mocked(apiClient.gatewayFetch).mockResolvedValue(
      new Response(JSON.stringify({ clientSecret: 'pi_x' }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );

    const mod = await importRoute();
    await mod.POST(
      makePostRequest({ amount: 100, agencyId: 'AG-forged' }),
    );

    const [, init] = vi.mocked(apiClient.gatewayFetch).mock.calls[0];
    const forwardedBody = JSON.parse(init?.body as string) as Record<
      string,
      unknown
    >;
    expect(forwardedBody).toEqual({ amount: 100 });
    expect(forwardedBody.agencyId).toBeUndefined();
  });
});

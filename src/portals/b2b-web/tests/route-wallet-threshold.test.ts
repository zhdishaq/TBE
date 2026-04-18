// Plan 05-05 Task 2 — /api/wallet/threshold route handler.
//
// Contract (T-05-05-03 IDOR mitigation + Pitfall 28):
//   - 403 when no session or no agency_id claim.
//   - 403 when role lacks 'agent-admin' (B2BAdminPolicy parity).
//   - PUT forwards Bearer via gatewayFetch; stripts body-supplied agencyId.
//   - 400 application/problem+json streamed through verbatim so the dialog
//     can parse `allowedRange.min=50`, `allowedRange.max=10000`.
//   - runtime = 'nodejs'.

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
  return await import('@/app/api/wallet/threshold/route');
}

function makePutRequest(body: unknown): Request {
  return new Request('http://localhost/api/wallet/threshold', {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
}

describe('/api/wallet/threshold route handler', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.mocked(authMod.auth).mockReset();
    vi.mocked(apiClient.gatewayFetch).mockReset();
  });

  it('declares nodejs runtime', async () => {
    const mod = await importRoute();
    expect(mod.runtime).toBe('nodejs');
  });

  it('returns 403 without agency_id', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { name: 'Ada' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    const mod = await importRoute();
    const res = await mod.PUT(
      makePutRequest({ thresholdAmount: 1000, currency: 'GBP' }),
    );
    expect(res.status).toBe(403);
    expect(apiClient.gatewayFetch).not.toHaveBeenCalled();
  });

  it('returns 403 when role is not agent-admin', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { agency_id: 'AG-1' },
      roles: ['agent-readonly'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    const mod = await importRoute();
    const res = await mod.PUT(
      makePutRequest({ thresholdAmount: 1000, currency: 'GBP' }),
    );
    expect(res.status).toBe(403);
  });

  it('forwards the happy-path 204 response', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { agency_id: 'AG-1' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    vi.mocked(apiClient.gatewayFetch).mockResolvedValue(
      new Response(null, { status: 204 }),
    );

    const mod = await importRoute();
    const res = await mod.PUT(
      makePutRequest({ thresholdAmount: 1000, currency: 'GBP' }),
    );
    expect(res.status).toBe(204);
    const [path, init] = vi.mocked(apiClient.gatewayFetch).mock.calls[0];
    expect(path).toContain('/wallet/threshold');
    expect(init?.method).toBe('PUT');
  });

  it('passes problem+json content-type through on 400', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { agency_id: 'AG-1' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    vi.mocked(apiClient.gatewayFetch).mockResolvedValue(
      new Response(
        JSON.stringify({
          type: '/errors/wallet-threshold-out-of-range',
          allowedRange: { min: 50, max: 10000, currency: 'GBP' },
          requested: 49,
        }),
        { status: 400, headers: { 'content-type': 'application/problem+json' } },
      ),
    );

    const mod = await importRoute();
    const res = await mod.PUT(
      makePutRequest({ thresholdAmount: 49, currency: 'GBP' }),
    );
    expect(res.status).toBe(400);
    expect(res.headers.get('content-type')).toBe('application/problem+json');
  });

  it('strips body-supplied agencyId before forwarding', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { agency_id: 'AG-jwt' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    vi.mocked(apiClient.gatewayFetch).mockResolvedValue(
      new Response(null, { status: 204 }),
    );

    const mod = await importRoute();
    await mod.PUT(
      makePutRequest({
        thresholdAmount: 1000,
        currency: 'GBP',
        agencyId: 'AG-forged',
      }),
    );

    const [, init] = vi.mocked(apiClient.gatewayFetch).mock.calls[0];
    const forwarded = JSON.parse(init?.body as string) as Record<string, unknown>;
    expect(forwarded.agencyId).toBeUndefined();
    expect(forwarded.thresholdAmount).toBe(1000);
    expect(forwarded.currency).toBe('GBP');
  });
});

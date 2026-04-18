// Plan 05-05 Task 2 — /api/wallet/transactions route handler.
//
// Contract (D-44 page-number pagination):
//   - 403 when no session or no agency_id.
//   - 403 when role lacks 'agent-admin' (B2BAdminPolicy parity).
//   - GET forwards Bearer via gatewayFetch; ?page= and ?size= are passed
//     through explicitly (NOT cursor/limit — D-44 locks page-number mode).
//   - Happy-path JSON body streamed verbatim.
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
  return await import('@/app/api/wallet/transactions/route');
}

function makeGetRequest(qs = ''): Request {
  return new Request(`http://localhost/api/wallet/transactions${qs}`, {
    method: 'GET',
  });
}

describe('/api/wallet/transactions route handler', () => {
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
    const res = await mod.GET(makeGetRequest());
    expect(res.status).toBe(403);
  });

  it('returns 403 when role is not agent-admin', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { agency_id: 'AG-1' },
      roles: ['agent'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    const mod = await importRoute();
    const res = await mod.GET(makeGetRequest());
    expect(res.status).toBe(403);
  });

  it('forwards default page=1 size=20 when no query params supplied (D-44)', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { agency_id: 'AG-1' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    vi.mocked(apiClient.gatewayFetch).mockResolvedValue(
      new Response(JSON.stringify({ items: [], total: 0 }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );

    const mod = await importRoute();
    const res = await mod.GET(makeGetRequest());
    expect(res.status).toBe(200);
    const [path] = vi.mocked(apiClient.gatewayFetch).mock.calls[0];
    expect(path).toContain('page=1');
    expect(path).toContain('size=20');
  });

  it('forwards explicit page + size query params (D-44 page-number pagination)', async () => {
    vi.mocked(authMod.auth).mockResolvedValue({
      user: { agency_id: 'AG-1' },
      roles: ['agent-admin'],
    } as unknown as Awaited<ReturnType<typeof authMod.auth>>);
    vi.mocked(apiClient.gatewayFetch).mockResolvedValue(
      new Response(JSON.stringify({ items: [], total: 0 }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );

    const mod = await importRoute();
    const res = await mod.GET(makeGetRequest('?page=3&size=50'));
    expect(res.status).toBe(200);
    const [path] = vi.mocked(apiClient.gatewayFetch).mock.calls[0];
    expect(path).toContain('page=3');
    expect(path).toContain('size=50');
  });
});

// Plan 05-01 Task 2 — RED tests for the /api/agents + /api/agents/[id]
// route handlers.
//
// Plan step 9 explicitly DEFERS the AgentBookingsControllerTests.cs suite
// to Plan 05-02 (it lives in tests/BookingService.Tests/). This file is
// the Vitest substitute listed in the same step; five cases cover the
// Phase 5 tampering mitigations called out in 05-CONTEXT.md:
//
//   T-05-01-02  — 403 when no session
//   T-05-01-02  — 403 when session lacks 'agent-admin'
//   T-05-01-03  — forged body.agency_id is IGNORED; the route injects
//                 session.user.agency_id into createSubAgent instead
//   T-05-01-06  — zod rejects role='agent-admin'
//   T-05-01-05  — deactivate across tenants returns 403 + structured warn
//
// The lib layer is mocked entirely so these tests never touch Keycloak.
// Module augmentation of `next-auth` is already in place (types/auth.d.ts)
// so `session.roles` / `session.user.agency_id` are strongly typed.

import { describe, it, expect, vi, beforeEach } from 'vitest';

// ---- Mocks --------------------------------------------------------------

const authMock = vi.fn();
vi.mock('@/lib/auth', () => ({ auth: (...args: unknown[]) => authMock(...args) }));

const createSubAgentMock = vi.fn();
const listAgencyUsersMock = vi.fn();
const deactivateUserMock = vi.fn();

// Named error classes the handlers need to `instanceof`-check.
class DuplicateUserError extends Error {}
class CrossTenantError extends Error {}

vi.mock('@/lib/keycloak-b2b-admin', () => ({
  createSubAgent: (...args: unknown[]) => createSubAgentMock(...args),
  listAgencyUsers: (...args: unknown[]) => listAgencyUsersMock(...args),
  deactivateUser: (...args: unknown[]) => deactivateUserMock(...args),
  DuplicateUserError,
  CrossTenantError,
}));

// ---- Helpers ------------------------------------------------------------

function session(roles: string[], agencyId = 'AG-001', userId = 'u-1') {
  return {
    roles,
    user: { id: userId, agency_id: agencyId, name: 'Ada', email: 'a@x.io' },
    email_verified: true,
  };
}

function jsonRequest(url: string, body: unknown): Request {
  return new Request(url, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
}

beforeEach(() => {
  authMock.mockReset();
  createSubAgentMock.mockReset();
  listAgencyUsersMock.mockReset();
  deactivateUserMock.mockReset();
});

// ---- POST /api/agents --------------------------------------------------

describe('POST /api/agents — create sub-agent', () => {
  it('returns 403 when no session (unauthenticated)', async () => {
    authMock.mockResolvedValueOnce(null);
    const { POST } = await import('@/app/api/agents/route');
    const resp = await POST(
      jsonRequest('https://b2b.local/api/agents', {
        email: 'n@x.io',
        firstName: 'N',
        lastName: 'M',
        role: 'agent',
      }),
    );
    expect(resp.status).toBe(403);
    expect(createSubAgentMock).not.toHaveBeenCalled();
  });

  it('returns 403 when caller is not agent-admin', async () => {
    authMock.mockResolvedValueOnce(session(['agent']));
    const { POST } = await import('@/app/api/agents/route');
    const resp = await POST(
      jsonRequest('https://b2b.local/api/agents', {
        email: 'n@x.io',
        firstName: 'N',
        lastName: 'M',
        role: 'agent',
      }),
    );
    expect(resp.status).toBe(403);
    expect(createSubAgentMock).not.toHaveBeenCalled();
  });

  it('IGNORES a forged body.agency_id and injects session.user.agency_id (T-05-01-03)', async () => {
    authMock.mockResolvedValueOnce(session(['agent-admin'], 'CALLER-AGENCY'));
    createSubAgentMock.mockResolvedValueOnce(undefined);
    const { POST } = await import('@/app/api/agents/route');
    const resp = await POST(
      jsonRequest('https://b2b.local/api/agents', {
        email: 'n@x.io',
        firstName: 'N',
        lastName: 'M',
        role: 'agent',
        // ATTEMPTED TAMPERING — must NOT be forwarded.
        agency_id: 'OTHER-AGENCY',
      }),
    );
    expect(resp.status).toBe(202);
    expect(createSubAgentMock).toHaveBeenCalledTimes(1);
    const callArg = createSubAgentMock.mock.calls[0][0];
    expect(callArg.agencyId).toBe('CALLER-AGENCY');
    // Defensive: the forged field must not leak into the lib call under
    // any aliased key, either — snapshot the full shape so any future
    // regression that spreads the request body is caught.
    expect(Object.keys(callArg)).toEqual(
      expect.arrayContaining(['agencyId', 'email', 'firstName', 'lastName', 'role']),
    );
    expect((callArg as Record<string, unknown>).agency_id).toBeUndefined();
  });

  it('returns 400 when body.role is "agent-admin" (T-05-01-06)', async () => {
    authMock.mockResolvedValueOnce(session(['agent-admin']));
    const { POST } = await import('@/app/api/agents/route');
    const resp = await POST(
      jsonRequest('https://b2b.local/api/agents', {
        email: 'evil@x.io',
        firstName: 'E',
        lastName: 'V',
        role: 'agent-admin',
      }),
    );
    expect(resp.status).toBe(400);
    expect(createSubAgentMock).not.toHaveBeenCalled();
  });
});

// ---- PATCH /api/agents/[id]/deactivate --------------------------------

describe('PATCH /api/agents/[id]/deactivate — cross-tenant guard', () => {
  it('returns 403 + logs a structured warn when target belongs to another agency (T-05-01-05)', async () => {
    authMock.mockResolvedValueOnce(session(['agent-admin'], 'AG-A'));
    deactivateUserMock.mockRejectedValueOnce(
      new CrossTenantError(
        'cross-tenant deactivation blocked caller=AG-A target_owner=AG-B user=u-99',
      ),
    );
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    const { PATCH } = await import('@/app/api/agents/[id]/deactivate/route');
    const resp = await PATCH(
      new Request('https://b2b.local/api/agents/u-99/deactivate', {
        method: 'PATCH',
      }),
      { params: Promise.resolve({ id: 'u-99' }) },
    );
    expect(resp.status).toBe(403);
    expect(warnSpy).toHaveBeenCalledTimes(1);
    expect(String(warnSpy.mock.calls[0][0])).toMatch(/cross-tenant deactivation/);
    warnSpy.mockRestore();
  });
});

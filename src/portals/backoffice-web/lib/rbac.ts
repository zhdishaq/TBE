// Plan 06-01 Task 3 — backoffice role predicates.
//
// Every mutation route handler and RSC page that gates on an ops-* role
// MUST import these predicates rather than inline the .includes() check,
// so the role taxonomy can be audited (and evolved) from a single file.
//
// D-46 / UI-SPEC: ops-admin > ops-finance > ops-cs > ops-read (precedence
// when multiple roles are held). Read access policies use inclusive
// predicates — e.g. wallet-credit READ is ops-finance OR ops-admin.

import type { Session } from 'next-auth';

export type OpsRole = 'ops-admin' | 'ops-cs' | 'ops-finance' | 'ops-read';

function roles(session: Session | null | undefined): string[] {
  return Array.isArray(session?.roles) ? (session?.roles ?? []) : [];
}

export function isOpsAdmin(session: Session | null | undefined): boolean {
  return roles(session).includes('ops-admin');
}

export function isOpsCs(session: Session | null | undefined): boolean {
  return roles(session).includes('ops-cs');
}

export function isOpsFinance(session: Session | null | undefined): boolean {
  return roles(session).includes('ops-finance');
}

export function isOpsRead(session: Session | null | undefined): boolean {
  const r = roles(session);
  // ops-read is the floor — every higher role implicitly satisfies it.
  return (
    r.includes('ops-read') ||
    r.includes('ops-cs') ||
    r.includes('ops-finance') ||
    r.includes('ops-admin')
  );
}

/**
 * Returns true iff the session holds AT LEAST ONE of the required roles.
 * Useful for middleware gates that accept multiple roles (e.g. wallet
 * credits list: ops-finance OR ops-admin).
 */
export function hasAnyRole(
  session: Session | null | undefined,
  required: readonly OpsRole[],
): boolean {
  const r = roles(session);
  return required.some((req) => r.includes(req));
}

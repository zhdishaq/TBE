// Plan 06-01 Task 4 — BO-09/BO-10 Dead-Letter Queue triage page.
//
// ops-admin only (middleware gate + defence-in-depth role check here).
// RSC renders the initial list server-side via gatewayFetch; the client
// component <DlqTable/> owns the 60s polling refresh, envelope viewer
// dialog, and Requeue/Resolve action dialogs.
//
// The backend DlqController is the authoritative role gate — the portal
// route-handler proxies + this RSC check are fail-fast defence layers.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsAdmin } from '@/lib/rbac';
import { DlqTable } from './dlq-table';

export const dynamic = 'force-dynamic';

type DlqListRow = {
  id: string;
  messageId: string;
  correlationId: string | null;
  messageType: string;
  originalQueue: string;
  failureReason: string;
  preview: string;
  firstFailedAt: string;
  lastRequeuedAt: string | null;
  requeueCount: number;
  resolvedAt: string | null;
  resolvedBy: string | null;
};

type DlqListResponse = {
  rows: DlqListRow[];
  totalCount: number;
  page: number;
  pageSize: number;
};

async function fetchInitialList(status: string): Promise<DlqListResponse> {
  try {
    const res = await gatewayFetch(
      `/api/backoffice/dlq?status=${encodeURIComponent(status)}&page=1&pageSize=20`,
      { method: 'GET' },
    );
    if (!res.ok) {
      return { rows: [], totalCount: 0, page: 1, pageSize: 20 };
    }
    return (await res.json()) as DlqListResponse;
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      redirect('/login');
    }
    return { rows: [], totalCount: 0, page: 1, pageSize: 20 };
  }
}

export default async function DlqPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsAdmin(session)) redirect('/dashboard');

  const sp = await searchParams;
  const status =
    sp.status === 'resolved' || sp.status === 'all' ? sp.status : 'unresolved';

  const initial = await fetchInitialList(status);

  return (
    <main className="mx-auto flex max-w-7xl flex-col gap-6 px-6 py-8">
      <header className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold">Dead-letter queue</h1>
        <p className="text-sm text-muted-foreground">
          MassTransit <code className="font-mono text-xs">_error</code>{' '}
          envelope triage. Requeue to live queue or resolve manually once
          remediated. Actions require <strong>ops-admin</strong>.
        </p>
      </header>

      <nav aria-label="DLQ status tabs" className="flex gap-2 border-b border-slate-200 dark:border-slate-800">
        <StatusTab href="/operations/dlq?status=unresolved" active={status === 'unresolved'}>
          Unresolved
        </StatusTab>
        <StatusTab href="/operations/dlq?status=resolved" active={status === 'resolved'}>
          Resolved
        </StatusTab>
        <StatusTab href="/operations/dlq?status=all" active={status === 'all'}>
          All
        </StatusTab>
      </nav>

      <DlqTable initial={initial} status={status} />
    </main>
  );
}

function StatusTab({
  href,
  active,
  children,
}: {
  href: string;
  active: boolean;
  children: React.ReactNode;
}) {
  return (
    <a
      href={href}
      aria-current={active ? 'page' : undefined}
      className={
        active
          ? 'border-b-2 border-slate-900 px-3 py-2 text-sm font-semibold dark:border-slate-200'
          : 'px-3 py-2 text-sm text-muted-foreground hover:text-foreground'
      }
    >
      {children}
    </a>
  );
}

// Plan 05-05 Task 5 — RequestTopUpLink.
//
// T-05-03-09 / T-05-05-02 mitigation: the non-admin "Request top-up" CTA
// MUST NOT leak any session material (agency_id, token, balance, threshold,
// session cookie) into the href that hands off to the user's default mail
// client. We emit ONLY a subject parameter; no body=, no query-string
// session data, no interpolated wallet numbers.
//
// Callers can pass an explicit `adminEmail`; otherwise we fall back to the
// static support inbox.

'use client';

export interface RequestTopUpLinkProps {
  adminEmail?: string;
  className?: string;
}

export function RequestTopUpLink({
  adminEmail,
  className,
}: RequestTopUpLinkProps): React.ReactElement {
  const email = adminEmail ?? 'support@thebookingengine.com';
  // Hard-coded subject — no interpolation. Prevents a future refactor from
  // sneaking session material into the mailto href (T-05-03-09 codified at
  // the code level).
  const href = `mailto:${email}?subject=${encodeURIComponent('Top-up request')}`;

  return (
    <a
      href={href}
      rel="noopener noreferrer"
      className={
        className ??
        'inline-flex h-9 items-center rounded-md border border-zinc-300 px-3 text-sm font-medium hover:bg-zinc-50'
      }
    >
      Request top-up
    </a>
  );
}

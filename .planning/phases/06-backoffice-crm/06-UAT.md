---
status: testing
phase: 06-backoffice-crm
source: 06-01-SUMMARY.md, 06-02-SUMMARY.md, 06-03-SUMMARY.md, 06-04-SUMMARY.md
started: 2026-04-20T00:00:00Z
updated: 2026-04-20T00:00:00Z
---

## Current Test

number: 1
name: Unified booking list across channels
expected: |
  Sign in to backoffice-web as an ops user. Visit /bookings. You see a tab strip
  (All/B2C/B2B/Manual), search + date filters, and a paged table. Clicking a row
  opens /bookings/{id} with summary, active cancellation cards, and event timeline.
awaiting: user response

## Tests

### 1. Unified booking list across channels (BO-01)
expected: /bookings shows All/B2C/B2B/Manual tabs, search + date filters, paged table; rows link to /bookings/{id} detail with BookingEvents timeline
result: [pending]

### 2. Staff cancel + 4-eyes approval (BO-03)
expected: ops-cs POSTs a cancellation on a booking; a different ops-admin approves it via /bookings/cancellations (self-approval returns 403); BookingCancellationApproved published
result: [pending]

### 3. Manual wallet credit with 4-eyes (D-39)
expected: ops-finance creates a wallet credit request (amount £0.01-£100,000, reason code); a different ops-admin approves within 72h TTL; WalletTransactions row with EntryType=ManualCredit appended; self-approval returns 403
result: [pending]

### 4. Dead-letter queue visibility + recovery (BO-09/10)
expected: /operations/dlq lists messages from the _error queue with full envelope; requeue re-publishes and increments RequeueCount; resolve records reason + resolver (ops-admin only)
result: [pending]

### 5. Append-only BookingEvents at DB level (BO-04/05)
expected: Every saga transition writes one row to dbo.BookingEvents with full Snapshot JSON; UPDATE/DELETE denied at SQL Server engine level for the booking_events_writer role
result: [pending]

### 6. Manual/offline booking entry (BO-02)
expected: /bookings/new 3-step wizard captures product + traveller + pricing, saves via /api/bookings/manual with Channel=Manual and CurrentState=Confirmed; appears in unified booking list with Manual chip; no GDS call
result: [pending]

### 7. Supplier contract CRUD (BO-07)
expected: /suppliers lists contracts with Upcoming/Active/Expired status chip; ops-finance can create/edit/soft-delete; reads under ops-read
result: [pending]

### 8. Payment reconciliation (BO-06)
expected: Nightly ReconciliationJob scans Stripe vs wallet for OrphanStripeEvent / OrphanWalletRow / AmountDrift / UnprocessedEvent; portal modal shows side-by-side JSON diff; ops-finance resolves with notes
result: [pending]

### 9. MIS reporting + CSV/Excel export (BO-08) — deferred
expected: /operations/mis runs volume/revenue/top-agents/top-routes report with CSV + Excel export
result: blocked
blocked_by: prior-phase
reason: Plan 06-03 shipped D-52 audit-log scaffold only; BO-08 controllers + exporters + portal + D-41 commission payouts + remaining D-52 CRUD deferred (see 06-03-SUMMARY.md "What Is NOT Done")

### 10. Markup rule CRUD + audit log (D-38/D-52) — partial
expected: /agencies/markup-rules list + form; every mutation writes pricing.MarkupRuleAuditLog row (Created/Updated/Deactivated/Deleted) with actor, before/after JSON, reason
result: blocked
blocked_by: prior-phase
reason: Plan 06-03 shipped only the audit-log data layer (migration + POCO + DbContext); MarkupRulesController + portal pages deferred

### 11. Commission payout batch (D-41) — deferred
expected: Monthly job accrues commissions, generates QuestPDF agency statement, 4-eyes approval gate; /finance/commission-payouts portal
result: blocked
blocked_by: prior-phase
reason: Plan 06-03 deferred D-41 entirely

### 12. Customer 360 (CRM-01)
expected: /customers lists customers with erasure filter; /customers/{id} 360 page shows Overview / Bookings / Communications / Audit; events drive lifetime stats via CRM projections
result: [pending]

### 13. Credit-limit enforcement at WalletReserveCommand (CRM-02 / D-61)
expected: agency with balance=0 + CreditLimit=100 can reserve up to £100; reserve £101 returns HTTP 402 problem+json `type=/errors/wallet-credit-over-limit`; no WalletTransactions row written; ops-finance can PATCH /agencies/{id}/credit-limit with reason (audit row + AgencyCreditLimitChanged event)
result: [pending]

### 14. Agency 360 (CRM-03)
expected: /agencies lists agencies; /agencies/{id} 360 shows balance + credit limit chip + tabs (Overview/Bookings/Wallet/Markup/Commission/Agents/Communications); credit-limit dialog wired to PATCH endpoint
result: [pending]

### 15. Communication log (D-62)
expected: ops-cs POSTs a markdown communication entry for a Customer or Agency (body ≤10000 chars, non-empty); entry persists in crm.CommunicationLog; GET returns paged rows DESC by CreatedAt; ops-read receives 403 on POST
result: [pending]

### 16. Upcoming trips (CRM-05)
expected: /trips/upcoming lists future-dated bookings filterable by status; past bookings excluded
result: [pending]

### 17. Global search (CRM-03)
expected: Cmd/Ctrl+K opens a cmdk dialog; typing fans out across Booking / Customer / Agency / PNR indices with debounced 250ms search; results grouped by kind; selecting navigates to /bookings/{id}, /customers/{id}, or /agencies/{id}
result: [pending]

### 18. GDPR erasure end-to-end (COMP-03 / D-57)
expected: ops-admin opens /customers/{id}, clicks "Erase customer data"; Radix AlertDialog requires typed-confirm of exact email + reason (10-500 chars); POST returns 202 with requestId; CRM NULLs Customer.Email/Name/Phone + writes tombstone + publishes CustomerErased; BookingSagaState PII goes NULL across all bookings but BookingEvents rows remain untouched (D-49); /customers/erasures archive shows the tombstone
result: [pending]

### 19. GDPR erasure blocked by open saga
expected: Attempting to erase a customer with any BookingSagaState NOT IN ('Confirmed','Cancelled','Failed') returns 409 problem+json `type=/errors/customer-erasure-blocked-open-saga` with the blocking bookingId
result: [pending]

### 20. GDPR erasure idempotent + typed-email guard
expected: Wrong typedEmail returns 400 `type=/errors/customer-erasure-typed-email-mismatch`; second erasure attempt on already-tombstoned email returns 409 `type=/errors/customer-already-erased` with original erasure date
result: [pending]

## Summary

total: 20
passed: 0
issues: 0
pending: 17
skipped: 0
blocked: 3

## Gaps

<!-- populated as issues are reported during interactive UAT -->

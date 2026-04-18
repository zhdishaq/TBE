# Phase 6: Backoffice & CRM - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-19
**Phase:** 06-backoffice-crm
**Areas discussed:** Scope + plan split, Backoffice portal + RBAC, Event sourcing + audit log, Money flows (D-38/39/41 + BO-06), Tactical (BO-02/COMP-03/BO-09/BO-08/CRM-02/CRM-04)

---

## Scope + plan split

### Q1: Where should Phase-5 deferrals (D-38 / D-39 / D-41) + COMP-03 land?

| Option | Description | Selected |
|--------|-------------|----------|
| Fold into 4 plans | Keep roadmap's 4-plan shape; distribute deferrals across existing plans | ✓ |
| Add fifth plan for money | Roadmap's 4 + a dedicated money-flows plan | |
| Defer some to Phase 7 | Push D-41 and/or COMP-03 to Phase 7 | |

**User's choice:** Fold into 4 plans
**Notes:** Matches precedent (Phase 04 + 05 each ran 4–6 plans); tighter phase shape.

### Q2: Which requirements feel scope-excessive for an MVP Phase 6?

| Option | Description | Selected |
|--------|-------------|----------|
| All in | Ship all 16 + 3 deferrals | ✓ |
| Defer supplier contracts (BO-07) | No hotel flow live | |
| Defer MIS Excel/PDF exports | CSV only | |
| Defer CRM communication log (CRM-04) | Staff notes later | |

**User's choice:** All in
**Notes:** No cuts — heavy phase accepted.

---

## Backoffice portal + RBAC

### Q1: Role set for tbe-backoffice realm

| Option | Description | Selected |
|--------|-------------|----------|
| 4 roles (ops-admin/cs/finance/read) | Mirrors B2B 3-role + finance split | ✓ |
| 2 roles (ops-admin + ops-user) | Flatter; audit log is the control | |
| 5 roles (split CS from booking-edit) | Tighter separation of duties | |

**User's choice:** 4 roles

### Q2: Portal fork source + auth model

| Option | Description | Selected |
|--------|-------------|----------|
| Fork b2b-web, Auth.js v5 | New `src/portals/backoffice-web/` at `basePath: /backoffice`, slate-900 accent, no Stripe CSP | ✓ |
| Fork b2c-web | b2c has permissive Stripe CSP — more to strip | |
| Single portal + role routing | Collapse all three portals | |

**User's choice:** Fork b2b-web, Auth.js v5

### Q3: 4-eyes approval scope (multi-select)

| Option | Description | Selected |
|--------|-------------|----------|
| None — audit log is the control | Every mutation attributed + reason-logged | ✓ |
| GDPR erasure (COMP-03) | 2-admin cosign on irreversible erasure | |
| Manual wallet credit (D-39) | 2-person cosign on refund credits | ✓ |
| Booking cancellation | 2-person cosign on confirmed-booking cancels | ✓ |

**User's choice:** Audit-log baseline, with 4-eyes overlay on wallet credit + booking cancellation. GDPR erasure stays single ops-admin.

---

## Event sourcing + audit log

### Q1: BO-04 DENY UPDATE/DELETE enforcement

| Option | Description | Selected |
|--------|-------------|----------|
| SQL Server role grants | Role with GRANT INSERT/SELECT; DENY UPDATE/DELETE | ✓ |
| AFTER UPDATE/DELETE trigger | RAISERROR + ROLLBACK | |
| SQL Server Temporal Tables | SYSTEM_VERSIONING | |

**User's choice:** SQL Server role grants

### Q2: BO-05 pricing-snapshot storage

| Option | Description | Selected |
|--------|-------------|----------|
| JSON column (nvarchar(max)) | Single Snapshot column; JSON_VALUE queryable | ✓ |
| Typed columns per event type | Migration per new event type | |
| Two tables (meta + blob) | Normalized with extra join | |

**User's choice:** JSON column

### Q3: CRM projection model

| Option | Description | Selected |
|--------|-------------|----------|
| MassTransit consumer + projection tables | Subscribe to BookingConfirmed etc. → CrmDbContext read models | ✓ |
| Direct cross-service DB views | Read-only DB user against other services | |
| Full event store (Marten / EventStoreDB) | Dedicated event-store engine | |

**User's choice:** MassTransit consumer + projection tables

---

## Money flows (D-38 / D-39 / D-41 + BO-06)

### Q1: D-38 Markup rule CRUD scope

| Option | Description | Selected |
|--------|-------------|----------|
| ops-finance only + bounded ranges + audit | Hard server-side bounds + MarkupRuleAuditLog | ✓ |
| ops-finance only, no hard bounds | Trust the role; audit captures after the fact | |
| ops-admin only | Highest role only; bottleneck on daily work | |

**User's choice:** ops-finance + bounded ranges + audit

### Q2: D-39 Post-ticket refund mechanic

| Option | Description | Selected |
|--------|-------------|----------|
| Manual wallet credit + reason code + 4-eyes | Ledger-only credit; 4-eyes per D-48 | ✓ |
| Stripe refund passthrough when available | Refund to original card if <120d old | |
| Automated compensation saga | Fare-rules refund calculator | |

**User's choice:** Manual wallet credit + reason code + 4-eyes

### Q3: D-41 Commission payout mechanic

| Option | Description | Selected |
|--------|-------------|----------|
| Monthly payout batch + statement | Month-close aggregation → ops-finance approve → WalletTransactions CommissionPayout + QuestPDF statement | ✓ |
| Offset against top-up | Credit commission at next top-up | |
| Per-booking immediate credit | Credit at BookingConfirmed | |

**User's choice:** Monthly payout batch + statement

### Q4: BO-06 payment reconciliation feed

| Option | Description | Selected |
|--------|-------------|----------|
| Stripe webhook + nightly reconciliation job | Persist every event into StripeEvents; nightly diff | ✓ |
| Daily Stripe Balance API pull | Pull balance transactions daily | |
| On-demand reconciliation only | Ad-hoc staff-triggered runs | |

**User's choice:** Stripe webhook + nightly diff job

---

## Tactical wrap-up

### Q1: BO-02 manual booking GDS question

| Option | Description | Selected |
|--------|-------------|----------|
| No GDS — manual-only record | Channel=Manual; staff enters supplier-ref + pax; no saga ran | ✓ |
| GDS-backed manual saga | Form triggers flight booking saga | |
| Both modes selectable | Toggle between modes | |

**User's choice:** No GDS

### Q2: COMP-03 GDPR erasure shape

| Option | Description | Selected |
|--------|-------------|----------|
| Staff-initiated + PII nulled + tombstone | ops-admin triggers; PII columns NULL; CustomerErasureTombstones records EmailHash + actor | ✓ |
| Customer self-service via B2C portal | Scope creep to Phase 4 frontend | |
| Hard delete + FK cascade | Violates BO-04 audit | |

**User's choice:** Staff-initiated + PII nulled + tombstone

### Q3: BO-09/10 DLQ semantics

| Option | Description | Selected |
|--------|-------------|----------|
| MassTransit _error queues + custom table | Tail _error queues; DeadLetterQueue table with requeue/resolve actions | ✓ |
| Saga-only dead-letter | Only saga compensation failures surface | |
| Manual dead-letter publishing | Each consumer catches + publishes | |

**User's choice:** MassTransit _error queues + custom table

### Q4: BO-08 MIS + CRM-02 + CRM-04 specifics (multi-select)

| Option | Description | Selected |
|--------|-------------|----------|
| CSV + Excel exports (ClosedXML) | Multi-sheet workbooks | ✓ |
| Credit limit enforced at booking reserve | Hard block, not alert-only | ✓ |
| Communication log = plain-text only | No attachments in v1 | ✓ |
| Daily MIS rollup table | Nightly aggregation for consistent perf | ✓ |

**User's choice:** All four (full tactical bundle).

---

## Claude's Discretion

Areas where Claude has latitude downstream:
- EF Core column types/nullability/indexes beyond the ones explicitly specified
- MassTransit contract reuse vs new contract types for approval/payout events
- Exact JSON shape inside the Snapshot column per event type
- MIS rollup job scheduling specifics (cron, timezone, catch-up)
- Monthly statement cut-over timing
- tbe-backoffice realm JSON delta
- backoffice-web navigation tree + page layouts (UI-SPEC phase to follow)
- AgencyStatement PDF design tokens
- Dead-letter envelope serialization format
- PaymentReconciliationQueue auto-resolve logic

## Deferred Ideas

- Customer-self-service GDPR erasure (Phase 7 candidate)
- MIS PDF export (Phase 7)
- Stripe card-refund passthrough (v2)
- Per-booking immediate commission credit or offset-against-top-up (rejected)
- Attachments on communication log (v2)
- Multi-dimensional markup rules (v2)
- Customer self-service markup editing (explicitly disallowed)
- Hard delete + FK cascade (rejected)
- 5-role RBAC (rejected)
- Full event-store engine (rejected)
- Real-time MIS without rollup (rejected)
- 4-eyes on GDPR erasure (declined)
- Hotel manual-booking integration (depends on Plan 04-03)

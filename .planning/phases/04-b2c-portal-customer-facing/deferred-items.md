# Phase 04 — Deferred Items

Items discovered during plan execution that are out of the current plan's scope but must be addressed in a later plan. Tracked per the GSD executor scope-boundary rule (only auto-fix issues directly caused by the current task's changes; log anything else here).

---

## Logged by 04-01 (2026-04-16)

### BookingDtoPublic missing `departureDate` and `productType`

- **Discovered by:** 04-01 Task 2 (dashboard partitioning + booking-row icons).
- **Context:** The B2C dashboard needs to split `/customers/me/bookings` results into Upcoming vs Past by departure date, and show a plane/bed/car icon per product type. Neither field is currently on `BookingDtoPublic` (see `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` line 161).
- **Current fallback:** Frontend `types/api.ts` declares both as optional. The dashboard partitioner uses `departureDate ?? createdAt`; `BookingRow` infers the product icon from the booking-reference prefix (`-HTL` → hotel, `-CAR` → car, else flight).
- **Resolution plan:** 04-02 (flight checkout) must populate `DepartureDate` onto `BookingSagaState` when a flight booking initialises, then expand `BookingDtoPublic` to include it. 04-03 does the same for hotels (`CheckInDate`) and the DTO gains a discriminator column `ProductType`.
- **Logged at:** `.planning/phases/04-b2c-portal-customer-facing/04-01-SUMMARY.md` §Open Items.

### `tbe-b2c-admin` client secret not provisioned

- **Discovered by:** 04-01 Task 2 (resend-verification relies on real `KEYCLOAK_B2C_ADMIN_CLIENT_SECRET`).
- **Context:** `infra/keycloak/realm-tbe-b2c.json` declares the client; `.env.example` declares the env var; no live Keycloak has been provisioned with the secret rolled into a developer `.env`. The 04-00 summary documents this as a blocker that 04-01 does not resolve.
- **Resolution plan:** Operator action — pull the secret from `Keycloak admin console → Clients → tbe-b2c-admin → Credentials` into the local `.env`. 04-02's `<pre_flight>` smoke test (`infra/keycloak/verify-audience-smoke.sh`) will surface the blocker when run.

### `server-only` dependency

- **Discovered by:** 04-01 Task 2 (writing `lib/keycloak-admin.ts`).
- **Context:** The recommended Next.js pattern for server-only modules is to `import 'server-only'`. The package is not installed in `src/portals/b2c-web/package.json`. We used a `typeof window !== 'undefined'` runtime guard instead to avoid expanding the dependency set mid-plan.
- **Resolution plan:** Add `server-only` in the next plan that does a general dependency review (04-04 checkout is the most likely candidate because it will add several new server-only helpers). Switch the runtime guard to `import 'server-only'` at that point.

## Logged by 04-03 (2026-04-16)

### ESLint flat-config broken — `@eslint/eslintrc` missing

- **Discovered by:** 04-03 Task 3 (pnpm lint after frontend tests).
- **Context:** `src/portals/b2c-web/eslint.config.mjs` imports `@eslint/eslintrc` which is not in `package.json` devDependencies. ESLint 9.39 throws `ERR_MODULE_NOT_FOUND`. Pre-existing — 04-02 did not hit this because its acceptance gate is `pnpm test`/`pnpm typecheck`, not `pnpm lint`.
- **Current fallback:** Typecheck (`pnpm typecheck`) is clean; tests (42/42) green. Lint is not a P04-03 acceptance gate.
- **Resolution plan:** Either add `@eslint/eslintrc` to devDependencies or migrate the config to pure flat config without the compat layer. Candidate plan: next infra/tooling pass (04-04 or later).


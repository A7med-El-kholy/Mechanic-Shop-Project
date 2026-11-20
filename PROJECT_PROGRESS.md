# PROJECT PROGRESS — MechanicShopWorkshopSystem

## Project architecture & technology stack
- Solution: `MechanicShop.slnx` (.NET 10, C# 13)
- .NET projects: `MechanicShop.Domain`, `MechanicShop.Application`, `MechanicShop.Infrastructure`, `MechanicShop.Api` (composition root)
- Frontend: Angular 21 at `src/MechanicShop.Web/` (NOT in .slnx)
- Database: SQL Server + EF Core 10, `AppDbContext : IdentityDbContext<AppUser>`
- Auth: ASP.NET Core Identity + JWT Bearer (roles: Manager, Technician, Customer) + Google OAuth (External cookie for sign-in)
- AI: `IAIService` abstraction + `MockAIService` (offline, rule-based) + `OpenAIService` (OpenAI-compatible, config-selectable)
- Tests: xUnit v3 (Domain 99 tests, Application 1 architecture test); Integration tests need Testcontainers/Docker (not available here)
- Central package management via `Directory.Packages.props`; `TreatWarningsAsErrors=true`

## Baseline (verified before changes)
- `dotnet build MechanicShop.slnx -c Release`: **SUCCEEDED, 0 warnings, 0 errors**
- Domain unit tests: **94 passed, 0 failed**
- Application unit tests: **1 passed, 0 failed**
- Integration tests: NOT runnable (no Docker/SQL Server in this environment)
- Angular build: NOT yet attempted

## Completed phases

### Phase 1 — Backend/DB stabilization
- Hardened `SeedData`: wrapped `ALTER DATABASE ... AUTO_CLOSE OFF` in try/catch so startup no longer crashes under non-sa users (logs a warning instead).
- Build green. Unit tests green.

### Phase 2/3 — Google Login (backend)
- `Microsoft.AspNetCore.Authentication.Google` (10.0.13) added (central package management + Api.csproj reference).
- `Program.cs`: registered the `External` cookie scheme + `AddGoogle` (ClientId/ClientSecret from `GoogleAuth:*`, `CallbackPath=/api/auth/signin-google`, `SaveTokens=true`; OnRemoteFailure → `/api/auth/google-failure`).
- `AuthEndpoints.cs`: added `GET /api/auth/google` (challenge → Google), `GET /api/auth/google-callback-complete` (reads External cookie → `FindOrCreateByGoogleAsync` → JWT → redirect to SPA with token in fragment), `GET /api/auth/google-failure`.
- `IIdentityService` + `IdentityService.FindOrCreateByGoogleAsync`: links Google by email (no duplicates), or creates a new Customer-only Google account (no password) + customer profile via `AspNetUserLogins.AddLoginAsync`.
- Config: `GoogleAuth.ClientId/ClientSecret` + `Frontend.BaseUrl` in appsettings.json; docker-compose + `.env.example` wired; `GoogleOAuth.md` created (Google Cloud Console checklist, redirect URIs, user-secrets/env setup, flow diagram, failure modes).

### Phase 2/4 — Google Login (frontend)
- `AuthStore.startExternalSession()`: stores token+expiry, fetches `/api/auth/me` for the user profile, navigates.
- New `AuthCallbackPage` (`/auth/callback`) + SCSS: reads `#access_token&expires_in&returnUrl` fragment, completes the session, navigates to the destination; guards against open redirects.
- Route `/auth/callback` added to `app.routes.ts`.
- Google button on the login page (`login.html`/`login.ts`/`login.scss`): calls `GET /api/auth/google?returnUrl=...`.
- `npm run build` SUCCEEDED.

### Phase 5 — AI integration
- `OpenAIService` (`src/MechanicShop.Infrastructure/AI/OpenAIService.cs`): real OpenAI-compatible provider. Structured `DiagnoseText`/`DiagnoseImage` (chat/completions, `response_format: json_object`, vision via base64 `image_url`), `Transcribe` (`/audio/transcriptions`), `Speak` (`/audio/speech` → base64 mp3), `GetVoices` (static catalogue).
- Selection: `Program.cs` factory — `AIService.Provider == "OpenAI"` + `AISettings:OpenAI:ApiKey` set → `OpenAIService`; otherwise → `MockAIService`. Both registered.
- Timeouts: `AddHttpClient("AI")` with `AISettings:OpenAI:TimeoutSeconds` (default 30). Cancellation flows from endpoints.
- Robustness: on provider error → log + fall back to Mock (assistant never breaks the shop UI). Diagnose/image parse JSON leniently. Keys only in backend config.
- `appsettings.json`: `AISettings` section; `Directory.Packages.props`/`MechanicShop.Infrastructure.csproj`: `Microsoft.Extensions.Http`. `AIProvider.md` created.

### Phase 7 — Code quality/security
- Refactored `OpenAIService` diagnose flow (Transcript = user description; ResponseText = human summary).
- Hardened `SanitizeFileName` (null/empty → "recording").
- Set `External` cookie `Path=/api` for reliable callback reads.
- Added integration tests (`AuthEndpointsTests`): `GoogleChallenge_WithoutCredentials_ReturnsValidationProblem`, `GoogleCallbackComplete_WithoutCookie_ReturnsUnauthorized`.

## Files created
- `PROJECT_AUDIT.md` (audit)
- `PROJECT_PROGRESS.md` (this file)
- `GoogleOAuth.md` (Google Cloud Console setup + flow)
- `AIProvider.md` (AI provider config + behavior)
- `src/MechanicShop.Web/src/app/pages/auth-callback/auth-callback.{ts,html,scss}` (new page)

## Files modified (backend)
- `src/MechanicShop.Infrastructure/Data/SeedData.cs` — defensive try/catch around AUTO_CLOSE.
- `Directory.Packages.props` — added `Microsoft.AspNetCore.Authentication.Google`, `Microsoft.Extensions.Http`.
- `src/MechanicShop.Api/MechanicShop.Api.csproj` — Google auth package ref.
- `src/MechanicShop.Api/Program.cs` — External cookie + AddGoogle + AI factory + HttpClient "AI".
- `src/MechanicShop.Api/Features/Auth/AuthEndpoints.cs` — Google endpoints.
- `src/MechanicShop.Application/Common/Interfaces/IIdentityService.cs` — added `FindOrCreateByGoogleAsync`.
- `src/MechanicShop.Infrastructure/Identity/IdentityService.cs` — implemented `FindOrCreateByGoogleAsync`.
- `src/MechanicShop.Api/appsettings.json` — `GoogleAuth`, `Frontend.BaseUrl`, `AISettings` sections.
- `src/MechanicShop.Infrastructure/AI/OpenAIService.cs` — new file.
- `src/MechanicShop.Infrastructure/MechanicShop.Infrastructure.csproj` — Microsoft.Extensions.Http ref.
- `docker-compose.yml` — Google + Frontend__BaseUrl wiring.
- `.env.example` — Google/Frontend placeholders.
- `PRODUCTION_README.md` — Google Login + AI provider sections.

## Files modified (frontend)
- `src/MechanicShop.Web/src/app/core/auth-store.ts` — `startExternalSession()`.
- `src/MechanicShop.Web/src/app/app.routes.ts` — `/auth/callback` route.
- `src/MechanicShop.Web/src/app/pages/login/login.{ts,html,scss}` — Google button + styles.

## Files modified (tests)
- `tests/MechanicShop.Api.IntegrationTests/AuthEndpointsTests.cs` — Google endpoint tests.

## Database and migration status
- Migrations: 13, from `20261021194157_InitialIdentity` to `20261023222008_AddAppointmentRequestTriage`. No new migrations needed for Google (AspNetUserLogins exists).
- Model snapshot consistent (build green, 0 warnings). Live-DB verification not possible (no SQL Server).

## Deployment (Docker, started 2026-11-20)
- `docker compose build` — built `mechanic-shop-api:latest` + `mechanic-shop-spa:latest`.
- `docker compose up -d` — started sql, api, spa. Verified: `/healthz` 200, `/` (SPA) 200, assets (main/styles/images/favicon) 200, auth smoke (login→/me→register) 200.
- **Bug fixed while up:** Google OAuth registered with empty creds → 500 on all requests (OAuthOptions.Validate). Fixed by conditional registration. API rebuilt + recreated.
- **Stale-page fix:** added `add_header Cache-Control "no-store" always` to nginx `location /` (root) so the Angular shell always comes fresh; rebuild + recreate spa. Verified: `GET /` → `Cache-Control: no-store`, `Content-Type: text/html`, `<app-root>` + Angular shell present.

## Build and test results (final)
- `dotnet build MechanicShop.slnx -c Release`: **SUCCEEDED, 0 warnings, 0 errors** (verified after backend + test changes)
- Domain unit tests: **94 pass, 0 fail**
- Application unit tests: **1 pass, 0 fail**
- Angular build (`npm run build`): **SUCCEEDED**
- Integration tests (AuthEndpoints + others): **NOT run** (no Docker/SQL Server in this environment). Added Google tests: `GoogleChallenge_WithoutCredentials_ReturnsValidationProblem`, `GoogleCallbackComplete_WithoutCookie_ReturnsUnauthorized`.
- Verified by inspection only: full Google OAuth browser flow (needs real Google creds + browser); OpenAI provider calls (needs API key).

## Known issues / limitations
1. Google full browser flow not E2E tested (no browser + real Google credentials available). Endpoints unit-tested for guards; challenge-with-credentials path returns 302 to Google by design.
2. OpenAI provider not E2E tested against a real key (integration needs credentials).
3. `SeedData` logs a warning instead of failing if the deployment user lacks ALTER DATABASE permission.
4. Dead empty dir `src/MechanicShop.WorkshopSystem/src/MechanicShop.Web/` (0 files, untracked) — harmless, not referenced.
5. Integration tests need Docker (Testcontainers + SQL Server); CI handles them (`ci.yml` runs `dotnet test`).
6. Frontend token in URL fragment — exposed to browser history but not to server logs; short-lived (60 min).
7. `.env` JWT_SECRET is a placeholder (not committed; replace locally).
8. Rate limiting on `/api/auth/google` is NOT applied (challenge endpoints) — noted as optional hardening; normal auth has it.

## Remaining tasks (in priority order)
1. Phase 6/8 — Frontend polish (responsive tweaks on auth pages; rely on existing motion.scss for animations; `prefers-reduced-motion` already handled).
2. Phase 8 — Final testing: run Angular build + unit tests again; try `npm run test` (vitest) if configured; confirm no new warnings.
3. Docs final: update `PROJECT_AUDIT.md` to reflect new state.
4. Optional: e2e smoke test of API via a quick `dotnet run` + curl (needs SQL Server; will document if runnable).

## Exact next steps to continue
1. Run Angular build + Domain/Application unit tests (final verification).
2. Optionally start API (`dotnet run` in src/MechanicShop.Api) — needs SQL Server running; if unavailable, skip with a note.
3. Update `PROJECT_AUDIT.md` to mark completed items.
4. Finalize `PROJECT_PROGRESS.md` and deliver.

### Audit pass update — 2026-11-20

- Fixed OpenAI-compatible provider requests to honor `AISettings:OpenAI:BaseUrl` and send the configured bearer key. Added distinct configurable chat, transcription, and speech models.
- Added a bounded, relevant retrieval step using only service and parts catalog data. No customer, payment, or full database data is sent to the model.
- Removed the live-provider Mock fallback. Invalid provider selection/missing OpenAI credentials fail fast; AI provider failures return 503/504.
- Restricted image inputs to JPEG/PNG/WEBP, validated extension/MIME/signature and the 10 MB cap, and preserved MIME in vision requests.
- Changed stock adjustments to reject overselling and integer overflow; added two domain tests.
- Added stable, additive startup seeds for eight parts, six services linked to those parts where applicable, four sample customers with vehicles, and three technician roster entries. Names/quantities already present are not overwritten.
- Updated `AIProvider.md` and replaced the stale root-cause report with `PROJECT_AUDIT.md`.
- Verification: Release solution build passed with 0 warnings/errors; 96 Domain tests and 1 Application test passed; Angular production build passed. Docker engine unavailable, so SQL integration tests, Compose startup, and seeded data against SQL Server remain unverified. Live AI and Google OAuth require credentials.
- Remaining gaps at that point: suppliers, purchase orders, payment ledger/partial payments, durable AI chat history, and persistent image uploads. Stock movement history was added in the continuation below.

### Inventory ledger continuation — 2026-11-20

- Added the `StockMovements` entity/table and EF migration. Existing positive balances are backfilled as opening balances; newly seeded parts receive an opening movement.
- Every manual adjustment, customer parts order, and completed work-order part consumption now records a movement with actor, reason, delta, before/after quantity, and timestamp.
- Added the staff-only `GET /api/parts/{partId}/movements` endpoint and inventory UI search/history panel. Added integration assertions for oversell rejection and shop/work-order movement history.
- Expanded demo seed flows with linked scheduled work orders and appointment requests. Demo data seeds automatically outside Production; Production requires explicit `Seed:DemoData=true`.
- Docker/Testcontainers remain unavailable, so integration tests cannot execute here.

### Payment and concurrency continuation — 2026-11-20

- Added SQL Server `rowversion` to parts and migration `AddPartRowVersion`; adjustment, checkout, and work-order completion now translate stale stock writes into clear 409 conflicts.
- New parts with initial quantity now record opening stock movement; existing positive balances are backfilled by migration.
- Card payment is now rejected in invoice and parts-order APIs when no gateway is configured. Portal screens offer cash reservation/selection only, and cash becomes paid only when a manager confirms receipt.
- Updated invoice, parts-order, notification, and reporting integration tests to cover the safe cash flow and card rejection. Final runtime verification remains blocked on the unavailable Docker engine.
- Final verification: solution build passed with 0 warnings/errors; Domain 96/96 and Application 1/1 unit tests passed; Angular production build and Compose configuration passed; EF reports no pending model changes. Docker integration tests remain unavailable.

### Supplier purchasing continuation — 2026-11-20

- Added supplier and purchase-order entities, migrations, manager endpoints, staff listing, and inventory UI for supplier creation, order creation, and one-time receiving.
- Receiving a purchase order increments stock and writes the linked movement records atomically; row-version conflicts return HTTP 409. Demo data includes a supplier and an open purchase order.
- Restricted manual inventory adjustments and purchase-order receiving controls to managers in the UI. Validated purchase quantity and cost before submission and tightened supplier email validation.
- Added domain coverage for purchase-order receipt idempotency and quantity bounds. Final project verification follows this entry.
- Verification: Release solution build passed with 0 warnings/errors; Domain 99/99 and Application 1/1 passed; Angular production build, Compose configuration, and EF pending-model check passed. SQL integration tests/live startup remain unavailable because Docker cannot run in this session.

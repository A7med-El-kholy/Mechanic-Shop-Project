# MechanicShopWorkshopSystem Audit

## Architecture verified

- Backend: .NET 10 / ASP.NET Core Minimal APIs, split into Domain, Application (MediatR + FluentValidation), Infrastructure, and Api composition root.
- Persistence: EF Core 10 with SQL Server and ASP.NET Identity in one `AppDbContext`; migrations are in Infrastructure.
- Frontend: Angular 21 standalone components, TypeScript, SCSS, and nginx container.
- Authentication: Identity password hashing and JWT bearer tokens; Manager, Technician, and Customer roles. Manager-only staff provisioning links a technician login to an employee roster entry.
- AI: `IAIService` with a deterministic Mock provider and a configurable OpenAI-compatible provider. Image and audio HTTP endpoints are protected by bearer auth.
- Containers: SQL Server, API, and SPA are configured in Docker Compose. The API applies migrations and safe additive seed operations at startup.
- Tests: .NET domain/application unit tests and SQL Server Testcontainers API integration tests. The Angular app has a production build.

## Findings verified from source

1. AI provider HTTP calls were missing configured bearer authorization and did not consistently use the configured base URL. Fixed by constructing authenticated requests against the configured endpoint.
2. AI diagnosis had no database-backed business context, and image requests were labeled JPEG for all formats. Fixed with bounded matching service/part retrieval and preservation of uploaded image media types.
3. A live AI provider failure could silently return a fabricated Mock diagnosis. Removed this fallback; invalid provider settings now fail startup, and provider failures return 503/504 responses.
4. Image upload accepted any `image/*` in the SPA and did not validate the server-side format. The UI/server now restrict to JPEG, PNG, and WEBP, cap size, and validate extension, MIME type, and basic file signatures.
5. Stock adjustment clamped overselling to zero, hiding an invalid stock-out. Domain logic now rejects negative results and integer overflow without mutating quantity.
6. Startup seed data only made roles and an optional manager. It now additively creates a small repeatable workshop catalog, four linked customer/vehicle records, and three roster technicians. Existing named records are retained; catalog quantities/prices are not overwritten.
7. Vehicle brand/model lookup tables and a payment ledger/partial payment records are not represented in the schema. Supplier and purchase-order entities are now implemented with frozen line costs and one-time receipt into the stock ledger. Payment remains full invoice/order status recording; customer card payment is rejected when no gateway is configured.
8. Voice chat uses browser SpeechRecognition/SpeechSynthesis. Server transcription and synthesis endpoints exist for the real provider, but the current chat UI does not call them. Mock audio is a sample transcript/silence and is not real voice processing.
9. Image data is sent for immediate AI analysis but is not persisted as an uploaded file/reference. Conversation history is held in the component, not durable server-side storage.
10. Full live-provider calls require provider credentials. Google OAuth browser completion also requires Google credentials. Technician accounts are securely provisioned by a manager, but no demo password is seeded.

## Implemented in this audit pass

- Corrected provider URL/auth headers for chat, transcription, and speech requests; separated chat/transcription/speech model configuration.
- Added relevant catalog retrieval from only service and parts tables; no customer or payment data is sent to AI.
- Removed the misleading live-provider Mock fallback and added explicit 503/504 handling.
- Added upload validation on both client and API, and made the vision data URI use the detected uploaded media type.
- Rejected stock-out operations that exceed available stock and protected against integer overflow.
- Added idempotent seed records for the currently modeled customer, vehicle, employee, part, and service entities.
- Added unit coverage for invalid stock movements.
- Added a stock movement ledger, opening-balance backfill migration, staff history endpoint, searchable history UI, and transaction records for manual adjustments, shop orders, and work-order consumption.
- Added SQL Server row-version concurrency checks so competing stock changes return HTTP 409 instead of overwriting balances.
- Disabled card checkout when no gateway is configured. Customer orders reserve for cash and invoices become paid only after manager-confirmed cash receipt.
- Added supplier management and purchase orders with manager-only creation/receiving, additive demo purchasing data, concurrency protection, and receipt movements recorded in the stock ledger.
- Expanded linked demo seed data with three scheduled repair jobs and three appointment requests. Demo records seed by default outside Production; Production requires `Seed:DemoData=true` explicitly.

## Verification

- `dotnet build MechanicShop.slnx -c Release`: passed, 0 warnings/errors.
- EF reports no pending model changes after all three new migrations.
- Domain unit tests: 99 passed, including purchase-order receipt idempotency and quantity bounds.
- Application unit tests: 1 passed.
- Angular production build: passed after supplier/purchase-order UI additions.
- `docker compose config --quiet`: passed.
- Docker/Testcontainers integration tests and live SQL startup were not run because the Docker engine pipe is unavailable in this session.
- Real OpenAI and Google OAuth flows were not verified because no external credentials were available.

## Remaining work

- Add a full payment ledger/partial payments; current payment recording only supports cash settlement of an entire invoice or shop order.
- Add vehicle brand/model lookup tables if normalized manufacturer/model reporting is required.
- Persist uploaded images securely with size/type controls and retention policy if the product needs attachments to outlive a request.
- Connect the chat UI to server transcription/synthesis if provider-based voice (rather than browser-local voice) is required.
- Add end-to-end tests against SQL Server and configured external providers in an environment with Docker and credentials.

# Northwind Support Desk

A .NET 10 Web API and React 19/TypeScript application built for the Verdentra technical assessment. It covers ticket filtering and paging, ticket triage and SLA rules, assignment, escalation history, JWT login, and a Docker-based local setup.

## Run locally

**Requirements:** .NET SDK 10, Node.js 20.19+ (Node 22 recommended), and Docker Desktop with Compose.

From the repository root:

```powershell
npm install
npm run db:up
```

Wait for SQL Server to report healthy. Configure local credentials through .NET User Secrets; never commit the values:

```powershell
dotnet user-secrets set "Authentication:BootstrapAdminEmail" "admin@northwind-support.local" --project apps/api/src/SupportDesk.Presentation
dotnet user-secrets set "Authentication:BootstrapAdminPassword" "<your-development-password>" --project apps/api/src/SupportDesk.Presentation
dotnet user-secrets set "Authentication:Jwt:SigningKey" "<random-key-at-least-32-UTF-8-bytes>" --project apps/api/src/SupportDesk.Presentation
```

Start the API and web app in separate terminals:

```powershell
npm run api
npm run web
```

The web app normally runs at <http://localhost:5173>; Swagger is available at `/swagger` on the API URL printed at startup. In Development, the API applies pending EF Core migrations and runs its seed process when it starts.

**Development login:** `admin@northwind-support.local`. The password is provided separately with the assessment submission and must be configured locally as `Authentication:BootstrapAdminPassword` or as `BOOTSTRAP_ADMIN_PASSWORD` for Docker. The bootstrap process creates the account only when email and password are configured and the email does not already exist; it does not reset an existing password. Use an assessment-only password.

## Defect and filtering

### DEFECT-117: filter requests

The ticket list should refetch immediately when a non-search filter changes, while free-text search remains debounced. The hook now compares the previous and current filter values and uses a delay only when search changes by itself. Status and SLA filter changes trigger an immediate request. Regression tests cover the debounce and immediate-filter behavior.

### Server-side filters

`TicketQueries.GetPagedAsync` composes predicates through `ApplyFilters` while the data is still an EF Core `IQueryable`. Filtering happens before `CountAsync`, sorting, `Skip`, and `Take`; filtered totals and page contents therefore use the same criteria. Supported filters are status, priority, category ID, customer ID, assigned agent ID, unassigned-only, search across title/reference/customer name, and SLA status. Filters combine with AND semantics; blank search is ignored.

To add a filter, add a typed property to `TicketQuery` and the frontend filter type, serialize it in the query builder, add an SQL-translatable predicate to `ApplyFilters`, and test it alone and together with another filter. The query structure keeps filtering in SQL rather than filtering a loaded page in memory. To inspect the generated query, run the API in Development, request an example such as `GET /api/tickets?priority=High&slaStatus=AtRisk`, and examine EF Core `Microsoft.EntityFrameworkCore.Database.Command` logs for the count and paged queries. A separate SQL-log excerpt for every filter combination was not retained.

## Triage, SLA, assignment, and escalation

- Requested priority defaults to **Medium**; categories that force Critical take precedence.
- Base SLA windows: **Low 72 h, Medium 24 h, High 8 h, Critical 4 h**. Premium customers receive half the window, with a one-hour minimum. The at-risk threshold defaults to 25% remaining and is configurable.
- Assignment considers active status, capacity, and specialist qualifications where required. Eligible agents are ordered by open-ticket load, then agent ID; when no agent qualifies, the ticket can remain unassigned.
- Escalation advances one priority level, recalculates assignment and SLA deadline, and stores a history row. Critical, Resolved, and Closed tickets cannot be escalated; the reason must be 5–500 characters.
- SLA status is derived from due time, resolution time, the start of the current SLA window, current time, and the configured threshold; it is not stored separately.

**Audit limitation to address:** the current escalation request includes an `EscalatedBy` value supplied by the client. For trustworthy attribution, the handler must instead derive the actor from the validated JWT principal. Do not treat the client-provided value as a secure audit identity.

## Authentication and security

`POST /api/auth/login` returns a bearer access token, expiry time, and user profile on success. Invalid credentials and inactive users use a generic authentication failure. Passwords are stored as salted PBKDF2-SHA256 hashes (600,000 iterations, 16-byte random salt, 32-byte derived hash), not plaintext.

JWT signing uses configured issuer, audience, expiry, and a key supplied outside source control. Use .NET User Secrets locally and environment variables for Docker. The React frontend stores the session in `sessionStorage`, sends `Authorization: Bearer ...`, and clears local session state on logout or an unauthorized response. `sessionStorage` is available to JavaScript and has XSS exposure; production should assess an `HttpOnly; Secure; SameSite` cookie session and suitable CSRF protection.

Protected ticket, customer, category, and agent controllers use `[Authorize]`, while login allows anonymous requests. A global fallback authorization policy is not configured; new controllers therefore need explicit protection. Role claims are issued, but role-specific policies have not been fully defined. Local Swagger login returned HTTP 200 during development; recheck login and a token-authorized request through the Docker API before submission.

## Database and design decisions

- **`SupportUsers`:** application users and password hashes, with a unique `Email` index to prevent duplicate login identities.
- **`TicketEscalations`:** from/to priority, assignment and SLA deadlines, reason, actor and timestamp; an index on `TicketId` supports history retrieval.
- Migrations live in `apps/api/src/SupportDesk.Infrastructure/Data/Migrations`. Apply migrations instead of deleting the database volume.
- Ticket reads use `AsNoTracking`, joins and narrow DTO projections. Filters are composed before count/paging.

Key design choices:

1. **Domain entity plus triage service:** ticket-state constraints and priority progression stay with the domain; shared SLA/assignment decisions live in a testable service used by creation and escalation, avoiding duplicated rules in controllers.
2. **Repository and unit of work:** persistence is separated from application handlers, and the ticket update and escalation history are committed in one unit of work.
3. **Query object and `IQueryable`:** `TicketQuery` collects read options and `TicketQueries` composes database filters/projections. A messaging bus or a larger CQRS framework was rejected as unnecessary for this assessment's scope.

## Docker: build and run

The root multi-stage `Dockerfile` builds with the .NET 10 SDK and runs the published API on the ASP.NET 10 runtime image. The runtime uses the image's non-root application user. Compose runs SQL Server and the API on the same network: the API connects to `sqlserver,1433`, while host tools use `localhost,14330`.

Configure the ignored root `.env` file with `MSSQL_SA_PASSWORD`, `JWT_SIGNING_KEY`, `BOOTSTRAP_ADMIN_EMAIL`, and `BOOTSTRAP_ADMIN_PASSWORD`. Use placeholders only in `.env.example`; do not commit `.env` or bake secrets into the image.

```powershell
docker compose config --quiet
docker compose up --build -d
docker compose ps
docker compose logs --tail 100 api
```

Swagger is available at <http://localhost:8080/swagger>. Stop containers while preserving the database with `docker compose down`; do not use `docker compose down -v` unless you intend to delete the volume.

**Observed Docker result:** the image built; `supportdesk-sql` reported healthy; `supportdesk-api` started and listened on port 8080. Logs showed SQL connectivity, migrations already up to date, and the seed process skipping demo ticket seeding because ticket data already existed. The exact image-size output was not recorded; capture it with `docker compose images api` before submission.

The Compose configuration is for assessment/development: it enables Development startup migration/seeding and Swagger. Production should disable demo seeding and Swagger, use HTTPS and restrictive CORS, use a secret manager, persist and protect Data Protection keys if required, and run migrations through a controlled deployment step rather than having each API replica migrate at startup.

## Tests and verification

Run at the repository root:

```powershell
npm run typecheck --workspace apps/web
npm run lint --workspace apps/web
npm test
npm run build
```

`npm test` runs .NET tests and frontend Vitest tests. The frontend tests cover debounced search, immediate ticket filters, ticket-table rendering, and escalation-history loading/submission. Separate .NET unit-test and architecture-test projects are included.

**Results observed during development:** the frontend suite reported 3 test files and 6 passing tests; the API build succeeded after authentication was added; Docker build and container startup succeeded; and local Swagger login returned HTTP 200. Rerun all listed commands and test a Docker-issued token against a protected endpoint after the final edits. These earlier results do not prove every end-to-end scenario has been tested.

## AI use

AI tools were used to discuss the debounce defect, propose query and triage changes, draft authentication/Docker work, and interpret TypeScript, ESLint, PowerShell, and build errors. The workflow was to inspect actual project files and diagnostics, make focused edits, then run checks or a manual request rather than assuming generated suggestions were correct.

Verification exposed two issues in suggested changes: React's hooks purity rule rejected calling `Date.now()` during render, so the expiry logic was moved into an effect/timer instead of disabling the rule; and TypeScript caught auth import paths that did not match the files' actual locations, so the imports were corrected. A regression test was also added for immediate SLA-filter requests. This description should be personalized with an example of a suggestion you changed or rejected and the exact check you used to verify it.

## Known limitations

- Escalation actor attribution must be derived from the authenticated principal; the client-supplied `EscalatedBy` field is not secure audit evidence.
- Authorization is applied at controller boundaries rather than through a global fallback policy.
- Browser `sessionStorage` is exposed to JavaScript; consider cookie-based auth and XSS/CSRF protections for production.
- Refresh tokens, token revocation, login rate limiting, account lockout, and fully defined role-specific policies are not implemented.
- Docker logs warned that default Data Protection keys may not persist outside the container and may be stored unencrypted. Configure protected, durable storage if those keys are required in production.
- A complete final test/build run, Docker login plus protected endpoint test, generated-SQL log excerpt, and exact image-size output should be recorded before submission.

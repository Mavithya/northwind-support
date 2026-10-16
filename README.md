# Northwind Support Desk

A ticket-management API and React web application built for the Verdentra technical assessment. The solution focuses on server-side filtering, ticket triage and SLA rules, escalation history, and JWT-based authentication.

> **Before submitting:** finish and verify the Docker steps described below; replace every `[VERIFY / FILL IN]` item with observed results; confirm the escalation actor is taken from the authenticated token (not a client-supplied form value); and confirm authentication is enforced centrally. Do not submit this draft with unresolved checks.

## 1. Prerequisites

- .NET SDK 10
- Node.js 20.19+ (Node 22 recommended)
- Docker Desktop with Compose

## 2. Run locally

From the repository root:

```powershell
npm install
npm run db:up
```

Wait for SQL Server to become healthy. Configure local secrets for the API. The development bootstrap email is `admin@northwind-support.local`; the password is the assessment development password supplied separately with the submission and must be set locally as a secret.

```powershell
dotnet user-secrets set "Authentication:BootstrapAdminEmail" "admin@northwind-support.local" --project apps/api/src/SupportDesk.Presentation
dotnet user-secrets set "Authentication:BootstrapAdminPassword" "<your-development-password>" --project apps/api/src/SupportDesk.Presentation
dotnet user-secrets set "Authentication:Jwt:SigningKey" "<random-key-at-least-32-UTF-8-bytes>" --project apps/api/src/SupportDesk.Presentation
```

Do not commit the actual password or JWT signing key. Start the API and frontend in separate terminals:

```powershell
npm run api
npm run web
```

In Development, the API applies pending EF Core migrations and seeds demo ticket data at startup. Open the frontend URL printed by Vite (normally `http://localhost:5173`). Swagger is available under `/swagger` on the API's listening URL.

## 3. Implemented behaviour

### DEFECT-117: ticket filters

The ticket list should fetch immediately when a non-search filter changes; free-text search remains debounced. The defect was caused by applying the debounce to filter changes generally, making status/priority/category/customer/assignment changes appear delayed. The hook now distinguishes a search-only change from changes to other filter fields, and the regression tests check that search waits for the debounce while other filters trigger a request immediately.

### Server-side filtering and paging

`TicketQueries.GetPagedAsync` composes the filters through `ApplyFilters` while the data is still an `IQueryable`. Filtering happens before `CountAsync`, sorting, `Skip`, and `Take`, so totals and page contents reflect the selected filters. Supported filters include status, priority, category, customer, assigned agent, unassigned-only, search across title/reference/customer name, and SLA status. Multiple filters combine with AND semantics. When adding a filter, add its typed field to `TicketQuery`/the frontend filter type, append it to the query string, add the predicate inside `ApplyFilters`, and add a regression test proving it composes with an existing filter.

**SQL verification:** `[VERIFY / FILL IN: state how you checked the generated SQL, e.g. EF Core command logging or a SQL-backed query test, and the test/query used. Do not claim the SQL was inspected unless you did it.]`

### Triage, SLA, assignment, escalation

- Requested priority defaults to Medium when omitted; categories configured to force Critical priority take precedence.
- Base SLA response windows are Low 72 hours, Medium 24 hours, High 8 hours, and Critical 4 hours. Premium customers receive half the base window, with a one-hour minimum. The at-risk threshold defaults to 25% remaining and is configurable.
- Assignment considers active agents, specialist qualifications when the category requires them, and capacity; when several agents qualify, the least-loaded agent is preferred with a stable ID tie-break. If no agent qualifies, the ticket can remain unassigned.
- Escalation advances one priority level at a time, recalculates the SLA deadline and assignment decision, records a history row, and cannot be performed on Critical, Resolved, or Closed tickets. The reason is validated to 5–500 characters.
- SLA status is derived from due time, resolution time, SLA-window start, current time, and the configured at-risk threshold; it is not stored as a separate mutable status.

`[VERIFY BEFORE SUBMISSION: make sure the escalation actor is derived server-side from a validated JWT claim. The current request/earlier UI included an EscalatedBy field supplied by the client; that is not suitable for the final security requirement. Remove it from the trusted request contract and use the authenticated principal instead.]`

## 4. Authentication and security

The login endpoint is `POST /api/auth/login`. Successful login returns a bearer access token, expiration time, and user profile. Invalid email/password and inactive-account cases should return the same generic authentication failure. Passwords are stored as salted PBKDF2-SHA256 hashes (600,000 iterations, 16-byte random salt, 32-byte derived hash); plaintext passwords are not stored.

The JWT uses issuer, audience, expiration, and a signing key configured outside source control. The development signing key is stored using .NET User Secrets for local runs and environment variables for Docker. Do not bake credentials or signing keys into the image or repository. The frontend currently stores the session in `sessionStorage`, attaches the token as `Authorization: Bearer ...`, and clears local session state on logout or an unauthorized response. `sessionStorage` is accessible to JavaScript and remains vulnerable to XSS; an `HttpOnly; Secure; SameSite` cookie-based session is a stronger production option.

**Authorization verification:** `[VERIFY BEFORE SUBMISSION: protected API endpoints must require authentication by default (prefer a central fallback policy), with only the login endpoint allowing anonymous access. Test that an unauthenticated request receives 401 and a valid token succeeds.]`

### Development login

- Email: `admin@northwind-support.local`
- Password: supplied separately in the assessment submission email; locally set it as `Authentication:BootstrapAdminPassword` (User Secrets) or `BOOTSTRAP_ADMIN_PASSWORD` (Docker `.env`).

The development account is created only when both bootstrap email and password are configured and the email does not already exist. The seed process does not overwrite an existing password. Use a throwaway assessment credential, not a personal or production password.

## 5. Database changes

- `SupportUsers`: stores application users and password hashes. A unique index on `Email` prevents duplicate login identities.
- `TicketEscalations`: stores the priority/assignment/SLA changes and reason for each escalation. An index on `TicketId` supports loading a ticket's history.
- EF Core migrations are maintained in `SupportDesk.Infrastructure/Data/Migrations`. Do not delete the database volume to apply a schema change; apply the migration so existing tickets are preserved.
- Ticket read queries use `AsNoTracking` and database-side joins/projections. Filters are added before count and paging to avoid returning an unfiltered page and filtering in memory.

## 6. Design decisions

1. **Domain behaviour in entities/services:** priority progression and ticket-state constraints belong to the domain, while triage decisions are grouped in a testable service shared by ticket creation/escalation. This avoids duplicating SLA/assignment rules in controllers.
2. **Repository and unit of work:** repositories are used at the persistence boundary, and the ticket update plus escalation-history record are saved through one unit of work. This allows the workflow to be tested without coupling handlers to EF Core.
3. **Query object / IQueryable composition:** `TicketQuery` carries filter, sorting, and paging inputs; `TicketQueries` builds composable database expressions and projects only the fields needed by the list. This keeps filtering in SQL and avoids N+1 navigation loading.

The application uses command/query handlers for focused use cases, but does not introduce a separate messaging bus or a full CQRS infrastructure because this assessment does not need that complexity.

## 7. Docker: build and run

`[VERIFY / FILL IN AFTER THE DOCKER FILES ARE IMPLEMENTED AND TESTED]`

The intended local setup uses a multi-stage .NET 10 Dockerfile: SDK image for restore/build/publish and ASP.NET runtime image for execution. Compose runs SQL Server and the API on the same private network. From inside the API container, the SQL Server address must be `sqlserver,1433` (not `localhost,14330`); port `14330` is the host port for local tools.

Expected commands once `Dockerfile`, `.dockerignore`, and the API service in `docker-compose.yml` are present:

```powershell
docker compose config --quiet
docker compose up --build -d
docker compose ps
docker compose logs --tail 100 api
```

Swagger should then be reachable on the host port configured in Compose. Stop without deleting database data with `docker compose down`.

- **Image size:** `[FILL IN AFTER RUNNING docker compose images api]` once the final image is built; record the observed size, not an estimate.
- **Secrets:** supply `JWT_SIGNING_KEY`, `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_PASSWORD`, and the SQL Server password at runtime via an ignored local `.env` or deployment secret store. Keep `.env` out of Git.
- **Development vs production:** Development currently applies migrations and seeds demo rows at API startup and exposes Swagger. For production, run migrations as a deliberate deployment step (e.g. a reviewed, versioned SQL migration or a one-off migration job), disable development seeding/Swagger, use HTTPS, set restrictive CORS, and use a proper secret manager. Do not let each application replica race to migrate on startup.
- **Container user:** `[VERIFY the final Dockerfile uses the non-root runtime user and that the container starts successfully.]`

## 8. Testing and verification

Run from the repository root:

```powershell
npm run typecheck --workspace apps/web
npm run lint --workspace apps/web
npm test
npm run build
```

The frontend tests cover the ticket list hook's debounced search and immediate filter changes, ticket table rendering, and escalation panel history/submission. The API has unit and architecture test projects. Manual verification performed so far: login endpoint returned HTTP 200 in Swagger for the development admin account.

`[VERIFY BEFORE SUBMISSION: record the final test/build results after all auth and Docker changes. Add automated authentication tests for correct credentials, incorrect credentials, inactive accounts, token validation, and unauthenticated access to protected endpoints. State any tests that are not implemented rather than claiming them.]`

## 9. AI assistance and verification

AI tools were used as a coding assistant to reason through the filter debounce defect, outline server-side query composition, draft triage/escalation/authentication changes, and help diagnose TypeScript/ESLint/build issues. The workflow was to inspect the actual files and error output, make a focused change, then run type checking, linting, tests, build, or a manual HTTP/Swagger check.

Examples of verification and corrections:

- The React Hooks purity lint rule rejected calling `Date.now()` during render; the expiry check was moved into an effect/timer rather than disabling the rule.
- Import paths for `AuthContext`/`RequireAuth` needed correction after the initial placement did not match the repository layout.
- A frontend regression test was added for immediate SLA filter changes rather than relying only on a manual click-through.

`[PERSONALIZE BEFORE SUBMISSION: add an actual example of a suggestion you rejected or changed, one bug introduced or exposed by AI-generated code, and the exact test/run that verified the correction. Use your own words and only describe what really happened.]`

## 10. Known limitations and follow-ups

- Browser `sessionStorage` is vulnerable to script access if an XSS flaw exists; production should consider an `HttpOnly` cookie-based design and stronger CSRF/XSS protections appropriate to that architecture.
- Access-token revocation/refresh, login rate limiting, and account lockout are not implemented unless added separately.
- Role claims are issued, but role-specific permissions need explicit policies if Admin and SupportAgent should have different capabilities.
- Docker is a local assessment/development configuration until the final image, health, secrets, non-root execution, and migration strategy have been verified. It is not a production deployment recipe without the hardening listed above.

## 11. Assessment submission checklist

- [ ] Finish the actor-from-token and central authorization requirements.
- [ ] Verify Docker build/run, non-root execution, runtime secrets, migration behaviour, and observed image size.
- [ ] Replace all `[VERIFY / FILL IN]` notes and personalize the AI-use section honestly.
- [ ] Run typecheck, lint, all .NET/frontend tests, and full build; record the actual results.
- [ ] Ensure `.env`, User Secrets, passwords, and JWT signing keys are not staged or committed.
- [ ] Confirm defect and filtering work remain in separate focused commits; preserve the real history.
- [ ] Confirm `origin` points to your own fork, not the upstream assessment repository.
- [ ] Push the verified commits to your fork. For a private fork, add `madura@verdentra.com` and `pandukal@verdentra.com` as collaborators.
- [ ] Email the fork URL, development login credentials, and an honest estimate of time spent by area.

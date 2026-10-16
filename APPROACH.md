# Approach — Northwind Support Desk

This note describes the approach taken for each assessment task. It is written as a working summary from the implementation history and should be reviewed and personalized before submission.

## Task 1 — DEFECT-117 and server-side filtering

I first traced the ticket list hook and found that the timing of its request depended on filter changes. Free-text search should be debounced because users often type several characters in succession, but dropdowns and checkboxes should apply immediately. The fix compares the previous and current filter values and uses a delay only when search changes by itself. I added regression coverage for debounced search and immediate filter changes.

For the API, I kept filtering in the EF Core query instead of retrieving a page and filtering it in memory. The query composes the selected predicates before counting and paging. This preserves the filtered count and prevents returning tickets that do not match the selected conditions. I kept search across title, reference, and customer name, and added SLA status to the same filter path.

The next filter should follow the same vertical slice: define a typed API query property, expose it in the frontend filter type and control, serialize it into the request, compose the SQL-translatable predicate in `ApplyFilters`, and test it alone and together with another filter. The generated SQL should be inspected through EF Core command logging or a SQL-backed test before claiming a specific query shape was observed.

## Task 2 — Triage, SLA, assignment, and escalation

I grouped priority/SLA/assignment decisions in a triage service shared by ticket creation and escalation so the same rules are not copied into controllers. Domain methods enforce ticket state and priority progression. SLA status is derived from due time, resolution time, the start of the current SLA window, current time, and a configurable threshold. Escalation persists the ticket update and the audit/history record through one unit of work.

The chosen assignment strategy considers agent activity, specialist requirements, capacity, then open-ticket count and agent ID for stable ordering. When no candidate is eligible, the ticket remains unassigned rather than assigning an inactive or over-capacity agent.

The main trade-off is keeping the assessment implementation understandable rather than introducing a queue, distributed event bus, or separate workflow engine. Before submission, the escalation actor must be taken from the validated JWT principal instead of a client-supplied field; otherwise the audit field is not trustworthy.

## Task 3 — Authentication

I added a separate support-user entity rather than using customer records or assuming an agent record is an application login. Passwords are salted PBKDF2-SHA256 hashes. The login endpoint returns a signed bearer token with issuer, audience, expiry, and user claims. Signing keys and bootstrap credentials are configured through .NET User Secrets locally and environment variables for Docker, not source code.

The React app has a login form, auth context, route guard, token attachment in the shared API client, and logout. I chose `sessionStorage` as a straightforward assessment implementation; it is still accessible to JavaScript and has XSS risk, so a production design should evaluate an `HttpOnly; Secure; SameSite` cookie session and suitable CSRF protection. Before submission, complete and verify the central authorization policy and derive the escalation actor from token claims. Test valid/invalid credentials, inactive accounts, a missing/invalid token on protected routes, and a valid token on those same routes.

## Task 4 — Docker

I used a multi-stage Dockerfile so the .NET SDK is used in the build stage while the final image uses the ASP.NET runtime. The root build context is needed for the shared build configuration and central NuGet package versions. Compose keeps SQL Server and the API on the same internal network; the API connects to `sqlserver,1433`, whereas host tools use the published port `14330`.

The image was built and the API container started. SQL Server reported healthy; the API logs showed that the database was reachable and migrations were up to date. The application listened on port 8080. The Data Protection log warnings and the exact image size should be addressed/recorded as part of final verification. In production, migrations should run as a controlled deployment step rather than automatically on every API replica's startup.

## AI use and verification

AI was used as a coding and debugging assistant to review snippets, suggest implementation steps, and interpret compiler/lint errors. I used the repository contents and command output as the source of truth rather than assuming suggested paths or changes were correct.

For example, the React Hooks purity rule flagged a direct `Date.now()` call during render; the expiry logic was moved to an effect/timer rather than disabling the rule. TypeScript also exposed import paths that did not match the actual auth component locations. The paths were corrected and checked again. The SLA filter received a regression test proving a non-search filter triggers a request without waiting for the search debounce.

Before submitting, I should record the final exact commands and results for typecheck, lint, .NET/frontend tests, build, Docker login, and a protected request. I should also note one suggestion I consciously rejected or changed, in my own words, based on what I actually verified rather than inventing an example.

## Final checks before submission

- Finish the actor-from-token and centralized authorization changes.
- Run and record typecheck, lint, complete test suite, and full build.
- Verify Docker login and one protected API endpoint with the bearer token.
- Record the actual image size and inspect SQL logging for a representative filtered request.
- Confirm `.env` and User Secrets are not committed and the Git remote points to my own fork.

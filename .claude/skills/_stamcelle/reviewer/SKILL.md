---
name: reviewer
description: Code review for .NET/C# compliance services. Checklists for security, domain logic, C# quality and ASP.NET Core patterns.
user-invocable: true
context: fork
---

# Code Review — Transaction Compliance Service

## Role

Strict but fair code reviewer with 20 years of experience in security, .NET and financial domains. Finds real problems — not stylistic nuances. Gives concrete fixes.

## Process

1. **Read all relevant code** — never assess without having read the file
2. **Categorise findings:**
   - **Critical (C)**: Security holes, incorrect compliance logic, crash — MUST fix
   - **Serious (S)**: Missing validation, logical errors, poor error handling — SHOULD fix
   - **Moderate (M)**: Race conditions, inconsistency, missing tests — consider
   - **Low (L)**: Stylistic, informational — can be ignored
3. **Give APPROVED / CONDITIONALLY APPROVED / REJECTED**
4. **On re-review**: Check that fixes do not introduce new problems

## Findings Table

```markdown
| # | Severity | File:line | Description | Recommended fix |
|---|----------|-----------|-----------  |-----------------|
| 1 | C | ScreeningService.cs:42 | Brief description | Concrete fix |
```

## Checklist: Compliance Domain

- [ ] **Amount limits are exact**: 100 000 NOK = 10 000 000 øre. Check whether the limit is `>` or `>=` — spec decides, but consistency must be verified
- [ ] **No `decimal`/`double` for amounts**: only `long` (øre) — `decimal` can give precision errors in accumulation
- [ ] **Cumulative limit**: daily aggregate uses UTC date consistently — not LocalTime
- [ ] **Sanctions list match**: case-insensitive string comparison — ISO 3166-1 alpha-2 in uppercase but input may vary
- [ ] **PEP priority**: `PendingReview` is the correct status when PEP + amount threshold — not `Flagged`
- [ ] **Rule conflict**: strictest status wins (Rejected > PendingReview > Flagged > Approved) — check that pipeline implements this
- [ ] **Audit log**: ALL decisions are logged with TransactionId + RequestId — not only rejected ones
- [ ] **`IScreeningRule` contract**: pipeline iterates over all rules — no early return before all rules have run (except hard rejection where spec allows it)

## Checklist: Architecture Layer Boundaries

- [ ] **Domain logic in Core**: rules, pipeline, boundary values — never in Api or Infrastructure
- [ ] **Infrastructure isolated**: external dependencies (PEP mock, CountryLists, stores) — never directly in Core or Api
- [ ] **Api is thin**: endpoints delegate to Core via injected services — no business logic in endpoint handlers
- [ ] **No layer skipping**: Api never references Infrastructure implementations directly — only via interface defined in Core

## Checklist: C# / .NET Quality

- [ ] **No `any` equivalent (`object`, `dynamic`)**: use specific types
- [ ] **Nullable reference types**: no unnecessary `!`-assertions — verify that null is impossible, or use null check
- [ ] **Records for DTOs**: ScreeningRequest, ScreeningResponse, RuleResult should be `record` — not class
- [ ] **`async/await` all the way**: no `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` — deadlock risk in ASP.NET
- [ ] **`ILogger` structured logging**: `_logger.LogInformation("Screening {TransactionId} result: {Status}", ...)` — not string interpolation
- [ ] **No magic numbers**: amount limits and country lists are named constants
- [ ] **`using` statements**: all IDisposable resources in `using` or `await using`
- [ ] **Exported methods**: explicit return type — `public Task<ScreeningResponse>`, not `public async Task<...>`

## Checklist: Security

- [ ] **No hardcoded secrets**: API keys, connection strings, tokens never in code
- [ ] **Error messages to client**: generic — never stack traces, internal field names, DB errors
- [ ] **Input validation**: amount > 0, currency is ISO 4217 format, AccountId and Country are non-null/non-empty
- [ ] **No logging of sensitive data**: payment information is logged structured — not raw data in error messages
- [ ] **Global exception handler**: unexpected exceptions return 500 with generic message — not the default ASP.NET error page

## Checklist: ASP.NET Core

- [ ] **Schema validation on POST**: body validated via attributes or FluentValidation — not manual null check
- [ ] **Health endpoint exists**: `GET /health` → `{"status":"ok"}` without auth
- [ ] **Request ID middleware**: all responses include RequestId in header AND response body
- [ ] **`IScreeningRule` in DI**: all rules registered via `services.AddScoped<IScreeningRule, XxxRule>()` + `IEnumerable<IScreeningRule>` injection in pipeline

## Checklist: Testing

- [ ] **WebApplicationFactory used** for API tests — not just unit-testing controller methods
- [ ] **Boundary values tested**: exact boundary value (10 000 000 øre), below and above
- [ ] **Negative scenarios**: invalid input returns 400, not 500
- [ ] **Mock PEP service**: tests do not make real HTTP calls to external service
- [ ] **Test isolation**: no global state between tests — no `static` mutable fields

## Checklist: GitHub Actions

- [ ] **`dotnet format --verify-no-changes`** is included in CI
- [ ] **CI runs on push and PR**
- [ ] **Branch protection**: merge blocked on CI failure
- [ ] **Test results published**: `.trx` file or summary report in CI output

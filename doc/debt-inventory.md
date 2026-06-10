# Technical Debt Inventory — ACME Financial Payment Platform

Catalogue of known technical debt in the codebase. Organised by category.
Each item includes: what it is, where it lives, which rule it violates, and estimated fix effort (S = hours, M = 1–2 days, L = 3+ days).

---

## 1. Architecture — Layering Violations

| # | Debt | File : Line (approx) | Rule Violated | Effort |
|---|------|----------------------|---------------|--------|
| A1 | `PaymentDbContext` injected directly into `PaymentServiceManager` — service bypasses repository layer | `src/Services/PaymentService.cs:34` | Rule 1 (Layering), CLAUDE.md | M |
| A2 | `PaymentDbContext` injected into `ReportingHelper` — helper talks to DB directly | `src/Services/ReportingHelper.cs:23` | Rule 1 (Layering) | M |
| A3 | `PaymentDbContext` injected into `ReportController` — controller bypasses service and repository entirely | `src/Controllers/ReportController.cs:39` | Rule 1 (Layering), CLAUDE.md | M |
| A4 | `GetPaymentsForAccount` issues raw EF query inside service method | `src/Services/PaymentService.cs:155` | Rule 1 (Layering) | S |
| A5 | `CancelPayment` issues raw EF query inside service method | `src/Services/PaymentService.cs:175` | Rule 1 (Layering) | S |
| A6 | `GetDailyReport` in service issues raw EF queries, returns `DataTable` | `src/Services/PaymentService.cs:193` | Rule 1 (Layering) | M |
| A7 | `ReportController.GetDailyReport` issues multiple EF queries directly | `src/Controllers/ReportController.cs:77` | Rule 1 (Layering) | M |
| A8 | `ReportController.GetAccountSummary` issues multiple EF queries directly | `src/Controllers/ReportController.cs:131` | Rule 1 (Layering) | M |
| A9 | `ReportController.ExportPayments` issues EF query directly | `src/Controllers/ReportController.cs:190` | Rule 1 (Layering) | S |

---

## 2. Architecture — Interface and Naming Violations

| # | Debt | File : Line (approx) | Rule Violated | Effort |
|---|------|----------------------|---------------|--------|
| B1 | `PaymentServiceManager` has no interface — cannot be mocked, injected as abstraction, or swapped | `src/Services/PaymentService.cs:20` | Rule 5 (Interface-First), CLAUDE.md | S |
| B2 | `ReportingHelper` has no interface — concrete class injected in controller | `src/Services/ReportingHelper.cs:18` | Rule 5 (Interface-First) | S |
| B3 | Class name `PaymentServiceManager` uses forbidden suffix "Manager" | `src/Services/PaymentService.cs:20` | CLAUDE.md Naming | S |
| B4 | Class name `ReportingHelper` uses forbidden suffix "Helper" | `src/Services/ReportingHelper.cs:18` | CLAUDE.md Naming | S |
| B5 | `ReportController` injects `ReportingHelper` by concrete type, not interface | `src/Controllers/ReportController.cs:43` | Rule 5 (Interface-First) | S |
| B6 | `HttpClient` injected directly in `ReportController` — not via `IHttpClientFactory` | `src/Controllers/ReportController.cs:40` | Rule 4 (No new / DI only) | S |

---

## 3. Regulatory — Audit Logging (PSD2 / DORA)

| # | Debt | File : Line (approx) | Rule Violated | Effort |
|---|------|----------------------|---------------|--------|
| C1 | `GetPayment` returns customer data with no audit log | `src/Services/PaymentService.cs:57` | Rule 2 (Audit Logging) — PSD2 Art. 97 | S |
| C2 | `CreatePayment` completes financial transaction with no audit log | `src/Services/PaymentService.cs:136` | Rule 2 (Audit Logging) — PSD2 Art. 97 | S |
| C3 | `GetPaymentsForAccount` returns account history with no audit log | `src/Services/PaymentService.cs:151` | Rule 2 (Audit Logging) | S |
| C4 | `CancelPayment` cancels financial transaction with no audit log | `src/Services/PaymentService.cs:183` | Rule 2 (Audit Logging) — PSD2 Art. 97 | S |
| C5 | `ReportController.GetDailyReport` returns all payment data with no audit log | `src/Controllers/ReportController.cs:63` | Rule 2 (Audit Logging) | S |
| C6 | `ReportController.GetAccountSummary` returns account summary with no audit log | `src/Controllers/ReportController.cs:117` | Rule 2 (Audit Logging) | S |
| C7 | `ReportController.ExportPayments` exports bulk customer financial data with no audit log | `src/Controllers/ReportController.cs:165` | Rule 2 (Audit Logging) — DORA Art. 9 | S |

---

## 4. Regulatory — PII / Sensitive Data in Logs (GDPR)

| # | Debt | File : Line (approx) | Rule Violated | Effort |
|---|------|----------------------|---------------|--------|
| D1 | `CreatePayment` logs `fromAccount`, `toAccount`, and `amount` in structured log | `src/Services/PaymentService.cs:70` | Rule 6 (No Sensitive Data in Logs) — GDPR Art. 32, PCI DSS Req 3 | S |
| D2 | `GetPayment` logs account number and amount on fetch | `src/Services/PaymentService.cs:65` | Rule 6 | S |
| D3 | `GetPaymentsForAccount` logs account number | `src/Services/PaymentService.cs:152` | Rule 6 | S |
| D4 | `ReportController.GetDailyReport` logs high-value payment details | `src/Controllers/ReportController.cs:107` | Rule 6 | S |
| D5 | `ReportController.GetAccountSummary` logs account number and holder name | `src/Controllers/ReportController.cs:158` | Rule 6 | S |
| D6 | `ReportController.ExportPayments` logs account number in request log | `src/Controllers/ReportController.cs:167` | Rule 6 | S |
| D7 | `ReportingHelper` logs account numbers via `Console.WriteLine` | `src/Services/ReportingHelper.cs:65` | Rule 6 | S |
| D8 | `ReportingHelper.GenerateMonthlyAccountSummary` sends account number in SMTP email subject and body — PII in outbound email | `src/Services/ReportingHelper.cs:294` | Rule 6 — GDPR Art. 32 | S |
| D9 | `ReportingHelper.GenerateDailyPaymentReport` includes account numbers in report DataTable sent via email | `src/Services/ReportingHelper.cs:140` | Rule 6 — GDPR Art. 32 | S |

---

## 5. Regulatory — Payment Idempotency

| # | Debt | File : Line (approx) | Rule Violated | Effort |
|---|------|----------------------|---------------|--------|
| E1 | `CreatePayment` has no `Idempotency-Key` header support — duplicate POST retries create duplicate payments | `src/Services/PaymentService.cs:69` | Rule 10 (Idempotency Keys) — PSD2 Art. 97 | L |

---

## 6. Error Handling

| # | Debt | File : Line (approx) | Rule Violated | Effort |
|---|------|----------------------|---------------|--------|
| F1 | `GetPayment` throws `Exception` on not-found instead of returning `Result<T>.Fail(...)` | `src/Services/PaymentService.cs:62` | Rule 3 (Result<T>) | S |
| F2 | `CreatePayment` throws `ArgumentException` and `Exception` throughout | `src/Services/PaymentService.cs:80` | Rule 3 (Result<T>) | M |
| F3 | `CancelPayment` throws `Exception` on not-found | `src/Services/PaymentService.cs:177` | Rule 3 (Result<T>) | S |
| F4 | Fraud check exception swallowed — failure silently allows payment through (fail-open security regression) | `src/Services/PaymentService.cs:108` | Rule 3 / Security | M |
| F5 | Fire-and-forget `Task.Run` for SMS notification — exceptions completely invisible | `src/Services/PaymentService.cs:140` | CLAUDE.md Error Handling | M |
| F6 | `ReportingHelper.GenerateDailyPaymentReport` top-level catch swallows all exceptions | `src/Services/ReportingHelper.cs:165` | csharp-kodestandard.md | S |
| F7 | `ReportingHelper.GenerateMonthlyAccountSummary` top-level catch swallows all exceptions | `src/Services/ReportingHelper.cs:245` | csharp-kodestandard.md | S |
| F8 | `ReportController` exchange-rate fetch exception swallowed — EUR amounts silently wrong | `src/Controllers/ReportController.cs:100` | Rule 3 | S |
| F9 | `ReportController` account-holder name fetch exception swallowed | `src/Controllers/ReportController.cs:152` | Rule 3 | S |
| F10 | `ReportingHelper.GenerateDailyPaymentReport` inner email-send exception swallowed separately from outer catch — email failures completely invisible even when report generation succeeds | `src/Services/ReportingHelper.cs:180` | csharp-kodestandard.md | S |
| F11 | `ReportingHelper.GenerateMonthlyAccountSummary` inner email-send exception swallowed separately — same pattern | `src/Services/ReportingHelper.cs:303` | csharp-kodestandard.md | S |

---

## 7. CancellationToken Propagation

| # | Debt | File : Line (approx) | Rule Violated | Effort |
|---|------|----------------------|---------------|--------|
| G1 | `GetPayment` has no `CancellationToken` parameter | `src/Services/PaymentService.cs:50` | Rule 7 (CancellationToken) | S |
| G2 | `CreatePayment` has no `CancellationToken` parameter | `src/Services/PaymentService.cs:69` | Rule 7 | S |
| G3 | `GetPaymentsForAccount` has no `CancellationToken` parameter | `src/Services/PaymentService.cs:150` | Rule 7 | S |
| G4 | `CancelPayment` has no `CancellationToken` parameter | `src/Services/PaymentService.cs:165` | Rule 7 | S |
| G5 | `ReportingHelper.GenerateDailyPaymentReport` has no `CancellationToken` parameter | `src/Services/ReportingHelper.cs:50` | Rule 7 | S |
| G6 | `ReportingHelper.GenerateMonthlyAccountSummary` has no `CancellationToken` parameter | `src/Services/ReportingHelper.cs:185` | Rule 7 | S |
| G7 | `ReportController.GetDailyReport` action has no `CancellationToken` parameter | `src/Controllers/ReportController.cs:63` | Rule 7 | S |
| G8 | `ReportController.GetAccountSummary` action has no `CancellationToken` parameter | `src/Controllers/ReportController.cs:117` | Rule 7 | S |
| G9 | `ReportController.ExportPayments` action has no `CancellationToken` parameter | `src/Controllers/ReportController.cs:165` | Rule 7 | S |
| G10 | `PaymentServiceManager.GetDailyReport` has no `CancellationToken` parameter | `src/Services/PaymentService.cs:242` | Rule 7 | S |

---

## 8. Performance and Maintainability

| # | Debt | File : Line (approx) | Rule Violated | Effort |
|---|------|----------------------|---------------|--------|
| H1 | `GenerateDailyPaymentReport` loads all payments for a day into memory — no pagination | `src/Services/ReportingHelper.cs:64` | Performance | M |
| H2 | `ExportPayments` loads entire filtered result set into memory before streaming | `src/Controllers/ReportController.cs:190` | Performance | M |
| H3 | Magic string status values ("Pending", "Cancelled", "Completed") used throughout instead of `PaymentStatus` enum | Multiple files | csharp-kodestandard.md | M |
| H4 | `DateTime.Now` used in 8+ places instead of `DateTimeOffset.UtcNow` — timezone bug in date filtering | Multiple files | csharp-kodestandard.md | S |
| H5 | Balance mutation inside `PaymentServiceManager.CreatePayment` — no outbox pattern, race condition on concurrent payments | `src/Services/PaymentService.cs:128` | Rule E7 (Outbox) | L |
| H6 | Hardcoded URLs (`FraudApiUrl`, `SmsGatewayUrl`) as `const` — cannot change between environments | `src/Services/PaymentService.cs:29` | CLAUDE.md Config | S |
| H7 | Hardcoded SMTP credentials in `ReportingHelper` | `src/Services/ReportingHelper.cs:29` | Security | S |
| H8 | `DataTable` returned from reporting methods — not a typed domain object | Multiple files | csharp-kodestandard.md | M |
| H9 | `Console.WriteLine` used throughout `ReportingHelper` instead of `ILogger` | `src/Services/ReportingHelper.cs` (multiple) | csharp-kodestandard.md Logging | S |
| H10 | String interpolation in `_logger.LogInformation($"...")` throughout — not queryable in Seq/Kibana | Multiple files | csharp-kodestandard.md Logging | S |
| H11 | `ReportController.GetDailyReport` action method is ~90 lines | `src/Controllers/ReportController.cs:63` | Rule 9 (Thin Controllers) | M |
| H12 | `ReportController.GetAccountSummary` action method is ~85 lines | `src/Controllers/ReportController.cs:117` | Rule 9 (Thin Controllers) | M |
| H13 | `ReportController.ExportPayments` action method is ~80 lines | `src/Controllers/ReportController.cs:165` | Rule 9 (Thin Controllers) | M |
| H14 | `ExportRequest` record defined at bottom of `ReportController.cs` instead of in `Models/` | `src/Controllers/ReportController.cs:230` | csharp-kodestandard.md | S |
| H15 | Inline fraud scoring heuristic in `ReportController.GetAccountSummary` — duplicates domain knowledge | `src/Controllers/ReportController.cs:145` | SRP | M |

---

## 9. Testing

| # | Debt | File : Line (approx) | Rule Violated | Effort |
|---|------|----------------------|---------------|--------|
| I1 | `PaymentServiceManager` has zero unit tests — no interface means mocking requires reflection hacks | N/A | CLAUDE.md Testing | L |
| I2 | `ReportingHelper` has zero unit tests — no interface, DB and email mixed in makes isolation impossible | N/A | CLAUDE.md Testing | L |
| I3 | `ReportController` has zero unit tests — fat methods mixing HTTP, DB, and external HTTP calls | N/A | CLAUDE.md Testing | L |
| I4 | Fraud-check fail-open behaviour untested — no test verifies that a fraud API failure rejects the payment | N/A | CLAUDE.md Testing | M |
| I5 | Daily limit enforcement logic untested | N/A | CLAUDE.md Testing | S |
| I6 | Balance mutation race condition untested | N/A | CLAUDE.md Testing | M |

---

## Summary

| Category | Items | Critical (Regulatory) |
|----------|-------|-----------------------|
| Layering Violations | 9 | — |
| Interface / Naming | 6 | — |
| Audit Logging (PSD2/DORA) | 7 | 7 |
| PII in Logs / Email (GDPR) | 9 | 9 |
| Idempotency (PSD2) | 1 | 1 |
| Error Handling | 11 | — |
| CancellationToken | 10 | — |
| Performance / Maintainability | 15 | — |
| Testing | 6 | — |
| **Total** | **74** | **17** |

Regulatory items (marked Critical above) must be fixed before the next compliance review.
The remaining items represent architectural and maintainability debt that compounds over time.

> **Note on counting methodology:** Claude typically finds 85–95 items when analysing this codebase — counting each individual EF query call as a separate layering violation rather than the injection point. Both approaches are defensible. The 74-item count reflects injection-point-level violations (one item per class that violates the rule), not per-call. Use this difference as a discussion point in Exercise 1 debrief.

# Code Review Rules — ACME Financial Payment Platform

Read by Claude Code's automated PR review.
Rules marked 🔴 are regulatory requirements — violations block merge.

---

## CRITICAL — blocks merge

- [ ] 🔴 **Rule 1** Controller contains direct database calls (`DbContext`, `SqlConnection`, EF queries)
- [ ] 🔴 **Rule 2** Endpoint handles customer data without `auditLogger.LogAsync(userId, action, resourceId)` before return
- [ ] 🔴 **Rule 6** Sensitive data (account number, SSN, amount, card number) logged in plain text
- [ ] 🔴 **Rule 10** POST endpoint for financial transaction missing support for `Idempotency-Key` header
- [ ] Controller contains business logic (calculations, state transitions, validation rules)

## HIGH — must fix before merge

- [ ] **Rule 3** Service method throws exception to caller instead of returning `Result<T>`
- [ ] **Rule 4** `new` used on service or repository inside a class (must be injected via constructor)
- [ ] **Rule 5** Class injects concrete implementation instead of interface (`PaymentService` instead of `IPaymentService`)
- [ ] **Rule 8** EF entity exposed outside repository layer (returned to service or controller)
- [ ] `try/catch` in controller layer
- [ ] Missing input validation on public endpoints

## MEDIUM — should fix

- [ ] **Rule 7** Async method does not accept `CancellationToken ct = default` or does not pass token downstream
- [ ] **Rule 9** Controller action method exceeds 20 lines (likely layer leakage)
- [ ] Async method missing `Async` suffix
- [ ] Inconsistent naming (see `csharp-kodestandard.md`)
- [ ] `.Result` or `.Wait()` used on Task instead of `await`
- [ ] `DateTime.Now` used instead of `DateTimeOffset.UtcNow`

## LOW — informational

- [ ] Nullable reference types not annotated (`string` without `?` where null is possible)
- [ ] Structured logging missing (string interpolation instead of template + args)
- [ ] Missing XML documentation on public API endpoints
- [ ] `#region` used (split the class instead)

---

## What Claude should do on findings

For each violation:
1. Mark exact line
2. State severity and which rule is violated
3. Explain *why* this is a problem (not just "wrong")
4. Provide concrete code example of correct implementation

Do not comment on whitespace, formatting, or naming that already follows `.editorconfig`.

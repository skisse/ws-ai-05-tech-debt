---
paths:
  - "src/**/*.cs"
---

# Architecture Rules — ACME Financial (10 rules)

These rules are enforced by Claude in all code review and generation.
Rules marked 🔴 are regulatory requirements — violations are not style discussions.

---

## Rule 1 — Strict Layering

```
Controller → Service → Repository → Database
```

- Controllers: HTTP in/out, no logic, no DB
- Services: business logic, no DbContext
- Repositories: only layer with database access

Violation: DbContext, SqlConnection or EF queries in Controller or Service.

---

## Rule 2 — 🔴 Audit Logging on All Customer Data Endpoints (PSD2 / DORA)

All endpoints that read or write customer data **must** contain:

```csharp
await _auditLogger.LogAsync(userId, action, resourceId);
```

This is called **before** the response is returned. If missing, the endpoint is not complete.

Always include: who (`userId`), what (`action` e.g. "PAYMENT_CREATED"), what (`resourceId`).

---

## Rule 3 — Result<T> for Error Handling, Not Exceptions

Services never throw exceptions to the controller layer. All service methods return `Result<T>`.

```csharp
// ✅
public async Task<Result<Payment>> CreateAsync(...) { ... }

// ❌
public async Task<Payment> CreateAsync(...) // throws exception on error
```

Controllers map `Result<T>` to HTTP status codes. No `try/catch` in controllers.
Exceptions are allowed internally in repositories for unexpected infrastructure failures.

---

## Rule 4 — Dependency Injection via Constructor, Never new

Services, repositories and infrastructure classes are never instantiated with `new` inside classes.
Everything is registered in the DI container and injected via constructor.

```csharp
// ❌
public class PaymentService
{
    private readonly PaymentRepository _repo = new PaymentRepository(); // forbidden
}

// ✅
public class PaymentService(IPaymentRepository repo) { ... }
```

---

## Rule 5 — Interface-First for All Services and Repositories

All services and repositories are exposed via interface. No class injects concretes.

```csharp
// ✅
public class PaymentController(IPaymentService service) { ... }

// ❌
public class PaymentController(PaymentService service) { ... }
```

Interface names: `IXxxService`, `IXxxRepository`.
Implementations registered as `services.AddScoped<IPaymentService, PaymentService>()`.

---

## Rule 6 — 🔴 No Sensitive Data in Logs

Account numbers, SSNs, amounts, card numbers and passwords are never logged in plain text.
Use masking or reference ID.

```csharp
// ❌
_logger.LogInformation("Payment from {Account} of {Amount}", accountNumber, amount);

// ✅
_logger.LogInformation("Payment {PaymentId} initiated", payment.Id);
```

Reference: GDPR Article 32, PCI DSS Requirement 3.

---

## Rule 7 — CancellationToken Propagated Through the Entire Call Chain

All async public methods accept `CancellationToken ct = default`.
Token is passed to all underlying async calls.

```csharp
// ✅
public async Task<Result<Payment>> GetAsync(Guid id, CancellationToken ct = default)
{
    return await _repo.GetAsync(id, ct);
}
```

Stops at the first layer where it is not propagated further.

---

## Rule 8 — Domain Models Do Not Cross Layer Boundaries as DB Entities

Repository entities (EF classes) are never exposed directly to the service or controller layer.
Convert to domain objects or DTOs at the layer boundary.

```csharp
// ❌ — EF entity returned from repository to controller
public PaymentEntity Get(Guid id) { ... }

// ✅ — domain object returned
public Payment Get(Guid id) { return entity.ToDomainModel(); }
```

Prevents EF change-tracking and lazy loading from leaking out of the repository layer.

---

## Rule 9 — Controllers Are Thin — Max 20 Lines per Action Method

A controller action exceeding 20 lines likely contains logic that belongs in the service layer.
Move it.

Allowed in controller:
- Get userId from claims
- Call one service method
- Call auditLogger
- Map Result<T> to IActionResult

Not allowed: validation beyond `ModelState`, conditional calls to multiple services, loops.

---

## Rule 10 — 🔴 Idempotency Keys on Payment Endpoints

All POST endpoints that initiate financial transactions **must** support idempotency key in header:

```
Idempotency-Key: <uuid>
```

The server stores the result and returns the same response on repeated calls with the same key.
Prevents double payment on network failures / retry-storms.

```csharp
[HttpPost]
public async Task<IActionResult> CreatePayment(
    [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
    [FromBody] CreatePaymentRequest request)
{
    if (idempotencyKey.HasValue)
    {
        var cached = await _idempotencyService.GetCachedResultAsync(idempotencyKey.Value);
        if (cached != null) return Ok(cached);
    }
    // ... create payment
}
```

---

## Enforcement Matrix

| Rule | Hook | CLAUDE.md | REVIEW.md | CI/Roslyn |
|------|------|-----------|-----------|-----------|
| 1 Layering | 🔴 blocks | ✅ | ✅ CRITICAL | ✅ |
| 2 Audit logging | — | ✅ | ✅ CRITICAL | — |
| 3 Result<T> | — | ✅ | ✅ HIGH | — |
| 4 No new | — | ✅ | ✅ HIGH | ✅ Roslyn |
| 5 Interface-first | — | ✅ | ✅ HIGH | — |
| 6 Sensitive data in log | — | ✅ | ✅ CRITICAL | — |
| 7 CancellationToken | — | ✅ | ✅ MEDIUM | ✅ Roslyn |
| 8 Entity leakage | — | ✅ | ✅ HIGH | — |
| 9 Thin controllers | — | ✅ | ✅ MEDIUM | — |
| 10 Idempotency | — | ✅ | ✅ CRITICAL | — |

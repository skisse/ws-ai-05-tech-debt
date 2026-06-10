---
paths:
  - "src/**/*.cs"
  - "tests/**/*.cs"
---

# C# Coding Standard — ACME Financial

Based on Microsoft C# Coding Conventions, .NET Runtime team practices and banking-specific requirements.
Claude uses these as reference in all C# code review and generation.

---

## Naming

```csharp
// Classes, interfaces, methods, properties: PascalCase
public class PaymentService { }
public interface IPaymentRepository { }
public async Task<Result<Payment>> CreatePaymentAsync() { }
public Guid PaymentId { get; init; }

// Local variables, parameters: camelCase
var paymentId = Guid.NewGuid();
async Task Get(Guid paymentId) { }

// Private fields: _camelCase with underscore
private readonly IPaymentRepository _paymentRepository;

// Constants: PascalCase (not ALL_CAPS)
private const int MaxAmountNok = 1_000_000;

// Async methods: always Async suffix
public async Task<Payment> GetPaymentAsync() { }
```

Forbidden: `Manager`, `Helper`, `Utils`, `Data` as suffix on business classes.

---

## Types and Nullability

```csharp
// Enable nullable in .csproj
<Nullable>enable</Nullable>

// Annotate explicitly
public Payment? GetOrNull(Guid id) { }   // may return null
public Payment Get(Guid id) { }          // guaranteed not null

// Use record for immutable domain objects
public record Payment(Guid Id, decimal Amount, string Currency);

// Use primary constructor where it simplifies
public class PaymentService(IPaymentRepository repo, IAuditLogger audit) { }
```

Avoid `!` (null-forgiving operator) — find the root cause instead.

---

## Async/await

```csharp
// ✅ — always await, not .Result or .Wait()
var payment = await _repo.GetAsync(id, ct);

// ❌ — blocks thread, can cause deadlock
var payment = _repo.GetAsync(id).Result;

// ✅ — ConfigureAwait(false) in library code (not in ASP.NET controllers)
var data = await _repo.GetAsync(id).ConfigureAwait(false);

// ✅ — use ValueTask for hot paths that often return synchronously
public ValueTask<Payment?> GetFromCacheAsync(Guid id) { }
```

Do not use `async void` — only exception is event handlers.

---

## Records and Immutability

```csharp
// Domain objects and DTOs: record (immutable by default)
public record CreatePaymentRequest(
    string FromAccountNumber,
    string ToAccountNumber,
    decimal Amount,
    string Currency
);

// Mutation via with-expression, not mutation
var updated = payment with { Status = PaymentStatus.Completed };

// EF entities: class with init properties (EF requires mutable)
public class PaymentEntity
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
}
```

---

## Error Handling

```csharp
// Services return Result<T> — never throw to caller
public async Task<Result<Payment>> CreateAsync(CreatePaymentRequest req)
{
    if (req.Amount <= 0)
        return Result<Payment>.Fail("Amount must be positive");

    // ...
    return Result<Payment>.Ok(created);
}

// Exceptions only for unexpected infrastructure failures (not business validation)
// Always log exception with context before re-throw
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error creating payment {PaymentId}", id);
    throw;
}

// Do not use exceptions for flow control
// ❌
try { return int.Parse(input); } catch { return 0; }
// ✅
return int.TryParse(input, out var val) ? val : 0;
```

---

## LINQ and Queries

```csharp
// Prefer method syntax over query syntax
var active = payments
    .Where(p => p.Status == PaymentStatus.Pending)
    .OrderByDescending(p => p.CreatedAt)
    .Take(100)
    .ToList();

// Materialize with ToList()/ToArray() at the right point
// Do not return IQueryable outside the repository layer

// AsNoTracking for read operations in EF
var payments = await _context.Payments
    .AsNoTracking()
    .Where(p => p.CustomerId == customerId)
    .ToListAsync(ct);
```

---

## Dependency Injection and Lifetime

```csharp
// Register with correct lifetime
services.AddScoped<IPaymentService, PaymentService>();          // per request
services.AddSingleton<IExchangeRateCache, ExchangeRateCache>(); // single instance
services.AddTransient<IIdempotencyValidator, IdempotencyValidator>(); // new each time

// Do not inject Scoped into Singleton — causes captive dependency bug
// Use IServiceScopeFactory when Scoped is needed from Singleton
```

---

## Logging

```csharp
// Structured logging — not string interpolation
// ✅
_logger.LogInformation("Payment {PaymentId} created for customer {CustomerId}",
    payment.Id, customerId);

// ❌
_logger.LogInformation($"Payment {payment.Id} created");  // not queryable

// LogLevel selection
_logger.LogDebug(...)       // developer diagnostics, off in prod
_logger.LogInformation(...) // normal flow, important events
_logger.LogWarning(...)     // unexpected but handled
_logger.LogError(...)       // failures requiring action
_logger.LogCritical(...)    // system in critical state

// Never log sensitive data (see architecture rule 6)
```

---

## Testing

```csharp
// Naming: Method_Scenario_ExpectedResult
[Fact]
public async Task CreatePayment_NegativeAmount_ReturnsFailure()
{
    // Arrange
    var request = new CreatePaymentRequest("1234", "5678", -100, "NOK");

    // Act
    var result = await _service.CreatePaymentAsync(request, "user1");

    // Assert
    Assert.False(result.Success);
    Assert.Contains("positive", result.Error);
}

// Repository tests: in-memory SQLite, not mocks
// Service tests: mock repositories via interface
// Do not test implementation details — test behaviour
```

---

## Formatting (.editorconfig)

```ini
[*.cs]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8-bom
insert_final_newline = true

# Use var only when type is apparent
csharp_style_var_when_type_is_apparent = true
csharp_style_var_elsewhere = false

# Use expression body only for simple one-liners
csharp_style_expression_bodied_methods = when_on_single_line

# Braces always on new line (Allman style)
csharp_new_line_before_open_brace = all
```

---

## Forbidden in This Codebase

| Pattern | Use instead |
|---------|-------------|
| `Thread.Sleep()` | `await Task.Delay()` |
| `.Result` / `.Wait()` on Task | `await` |
| `dynamic` type | Concrete type or generics |
| `#region` | Split the class instead |
| `public` field | Property with `{ get; init; }` |
| `DateTime.Now` | `DateTimeOffset.UtcNow` |
| `Guid.Empty` as default | `Guid?` (nullable) |
| `catch (Exception)` without logging | Always log before re-throw |
| String concatenation in loops | `StringBuilder` or LINQ |

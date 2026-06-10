---
paths:
  - "src/Controllers/**"
  - "src/Services/**"
  - "src/Repositories/**"
---

# Rule: Layering

Controllers NEVER call the database directly. This applies to:
- `DbContext` or subclasses
- `SqlConnection`, `SqlCommand`
- Entity Framework queries (`_context.Set<T>()`, `.Query<T>()`)
- Dapper calls

If you see this in a Controller: move it to the Repository layer.

Example of WRONG:
```csharp
// PaymentController.cs — VIOLATION
var payment = await _dbContext.Payments.FindAsync(id); // ← ILLEGAL
```

Example of CORRECT:
```csharp
// PaymentController.cs
var result = await _paymentService.GetPaymentAsync(id); // ← CORRECT
```

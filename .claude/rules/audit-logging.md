---
paths:
  - "src/Controllers/**"
  - "src/Services/**"
---

# Rule: Audit Logging (PSD2 / DORA)

All controller endpoints that handle customer data **must** contain a call to `_auditLogger.LogAsync(...)`.

This is not a code style rule. It is a regulatory requirement under PSD2 Article 97 and DORA Article 9(1).

Missing audit logging → stop, add it.

The call must include:
- `userId` — who is performing the action
- `action` — what is being done (e.g. `"PAYMENT_CREATED"`)
- `resourceId` — which resource is affected

```csharp
await _auditLogger.LogAsync(userId, "PAYMENT_CREATED", payment.Id.ToString());
```

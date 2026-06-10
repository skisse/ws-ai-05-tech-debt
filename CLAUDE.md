# Architecture Rules — ACME Financial Payment Platform

## Layering (ENFORCED BY HOOK)

Strict three-layer architecture. No exceptions.

```
Controller → Service → Repository → Database
```

- **Controllers** receive HTTP requests, validate input, call one Service, return response. Zero business logic. Zero database calls.
- **Services** contain business logic. Call Repositories. Never DbContext directly.
- **Repositories** are the only layer that talks to the database. No business logic.

## Audit Logging (REQUIRED — PSD2 + DORA)

All endpoints that read or write customer data **must** call `_auditLogger.Log(...)` before the response is returned.

Pattern:
```csharp
await _auditLogger.LogAsync(userId, action, resourceId);
```

Missing audit logging is a regulatory violation, not a style preference.

## Error Handling

All service methods return `Result<T>` — never `throw` to the controller layer.
Controllers map `Result<T>` to HTTP status codes. No `try/catch` in controllers.

## Naming

- Repositories: suffix `Repository`, interface `IXxxRepository`
- Services: suffix `Service`, interface `IXxxService`
- Controllers: suffix `Controller`
- No `Manager`, `Helper`, `Utils`

## Dependency Injection

Everything injected via constructor. No `new` on services or repositories inside classes.
No static calls to business logic.

## Testing

All new service methods must have corresponding unit tests.
Repository tests run against in-memory SQLite, not mocks.

---

@.claude/rules/layering.md
@.claude/rules/audit-logging.md
@.claude/rules/arkitekturregler.md
@.claude/rules/csharp-kodestandard.md

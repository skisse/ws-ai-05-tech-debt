---
paths:
  - "src/Consumers/**"
  - "src/Events/**"
---

# Event-Driven Architecture Rules — ACME Financial

Rules for Kafka-based messaging in .NET services.
Rules marked 🔴 are regulatory or data-integrity requirements.

---

## Rule E1 — Topic Naming Convention

Format: `<domain>.<entity>.<event>`

```
payments.payment.created
payments.payment.cancelled
accounts.account.debited
```

All lowercase, dot-separated. Domain = bounded context. Entity = aggregate. Event = past tense.

Violation: `PaymentCreated`, `payment-events`, `payments_topic`

---

## Rule E2 — 🔴 Event Schema — Required Fields

All events must include:

```csharp
public record PaymentEvent(
    Guid EventId,          // unique per event — used for idempotency
    Guid CorrelationId,    // trace across services
    DateTimeOffset OccurredAt,
    int Version,           // schema version
    string EventType,      // e.g. "payment.created"
    Guid PaymentId         // domain payload
    // ... domain fields
);
```

Missing `EventId` makes idempotency impossible. Missing `CorrelationId` breaks distributed tracing.

---

## Rule E3 — 🔴 Consumer Idempotency

Every consumer must check whether the event has already been processed before acting.

```csharp
// ✅
if (await _idempotencyStore.HasBeenProcessedAsync(message.EventId))
    return;

await ProcessAsync(message);
await _idempotencyStore.MarkProcessedAsync(message.EventId);
```

Kafka delivers at-least-once. Without idempotency: duplicate payments, double debits.

---

## Rule E4 — Dead Letter Queue on All Consumers

Unhandled exceptions must route the message to a DLQ — never silently swallow or crash the consumer.

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process event {EventId}", message.EventId);
    await _dlqProducer.ProduceAsync("payments.payment.created.dlq", message);
}
```

DLQ topic naming: append `.dlq` to source topic.

---

## Rule E5 — Consumers Are Thin — No Business Logic, No Direct DB Access

Same principle as controllers:

```
Consumer → Service → Repository → Database
```

Consumers receive the event, call one service method, handle DLQ on failure. No EF queries, no business calculations, no conditional branching on domain state.

---

## Rule E6 — 🔴 Audit Logging on Events Affecting Customer Data

Any event that reads or modifies customer financial data must log via `_auditLogger.LogAsync`.

```csharp
await _auditLogger.LogAsync(message.InitiatedByUserId, "PAYMENT_PROCESSED", message.PaymentId.ToString());
```

Same PSD2/DORA requirement as HTTP endpoints — the transport is different, the obligation is not.

---

## Rule E7 — Outbox Pattern for Producers

Services must not produce Kafka events directly inside a database transaction.
Use the outbox pattern: write event to an outbox table in the same transaction, relay publishes asynchronously.

```csharp
// ✅ — outbox in same transaction
await _paymentRepository.CreateAsync(payment);
await _outbox.EnqueueAsync(new PaymentCreatedEvent(payment.Id, ...));
await _unitOfWork.CommitAsync();

// ❌ — event published before transaction commits
await _kafkaProducer.ProduceAsync("payments.payment.created", evt);
await _paymentRepository.CreateAsync(payment);
```

Prevents ghost events (event published, DB write fails) and lost events (DB write succeeds, Kafka fails).

---

## Enforcement Matrix

| Rule | CLAUDE.md | Code review | Hook |
|------|-----------|-------------|------|
| E1 Topic naming | ✅ | ✅ HIGH | — |
| E2 Event schema | ✅ | ✅ CRITICAL | — |
| E3 Idempotency | ✅ | ✅ CRITICAL | — |
| E4 DLQ | ✅ | ✅ CRITICAL | — |
| E5 Thin consumer | ✅ | ✅ HIGH | — |
| E6 Audit logging | ✅ | ✅ CRITICAL | — |
| E7 Outbox pattern | ✅ | ✅ HIGH | — |

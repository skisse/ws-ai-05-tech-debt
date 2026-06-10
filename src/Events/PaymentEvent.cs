namespace AcmeFinancial.Payments.Events;

public record PaymentCreatedEvent(
    Guid EventId,           // unique per event — used for idempotency
    Guid CorrelationId,     // trace across services
    DateTimeOffset OccurredAt,
    int Version,            // schema version
    string EventType,       // "payment.created"
    Guid PaymentId,
    string FromAccountNumber,
    string ToAccountNumber,
    decimal Amount,
    string Currency,
    string InitiatedByUserId
);

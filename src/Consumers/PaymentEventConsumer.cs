using AcmeFinancial.Payments.Events;
using AcmeFinancial.Payments.Services;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;

namespace AcmeFinancial.Payments.Consumers;

public class PaymentEventConsumer(
    IConsumer<string, PaymentCreatedEvent> consumer,
    IPaymentService paymentService,
    IIdempotencyStore idempotencyStore,
    IProducer<string, PaymentCreatedEvent> dlqProducer,
    IAuditLogger auditLogger,
    ILogger<PaymentEventConsumer> logger
) : BackgroundService
{
    private const string Topic = "payments.payment.created";
    private const string DlqTopic = "payments.payment.created.dlq";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        consumer.Subscribe(Topic);

        while (!ct.IsCancellationRequested)
        {
            var result = consumer.Consume(ct);
            await HandleAsync(result.Message.Value, ct);
        }
    }

    private async Task HandleAsync(PaymentCreatedEvent message, CancellationToken ct)
    {
        try
        {
            // Rule E3: idempotency check before any processing
            if (await idempotencyStore.HasBeenProcessedAsync(message.EventId, ct))
                return;

            // Rule E5: delegate to service — no business logic here
            var result = await paymentService.ProcessPaymentEventAsync(message.PaymentId, ct);

            if (!result.Success)
            {
                logger.LogWarning("Payment event {EventId} rejected: {Error}", message.EventId, result.Error);
                return;
            }

            // Rule E6: audit logging for customer data
            await auditLogger.LogAsync(
                message.InitiatedByUserId,
                "PAYMENT_EVENT_PROCESSED",
                message.PaymentId.ToString()
            );

            await idempotencyStore.MarkProcessedAsync(message.EventId, ct);
        }
        catch (Exception ex)
        {
            // Rule E4: route to DLQ — never swallow or crash
            logger.LogError(ex, "Failed to process event {EventId} — routing to DLQ", message.EventId);
            await dlqProducer.ProduceAsync(DlqTopic, new Message<string, PaymentCreatedEvent>
            {
                Key = message.PaymentId.ToString(),
                Value = message
            });
        }
    }
}

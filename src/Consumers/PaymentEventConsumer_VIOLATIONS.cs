// ╔══════════════════════════════════════════════════════════╗
// ║  DEMO FILE: Contains deliberate architecture violations  ║
// ║  Used in workshop to show what Claude blocks             ║
// ╚══════════════════════════════════════════════════════════╝

using AcmeFinancial.Payments.Events;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace AcmeFinancial.Payments.Consumers;

public class PaymentEventConsumer(
    IConsumer<string, PaymentCreatedEvent> consumer,
    PaymentDbContext dbContext    // VIOLATION E5: DbContext injected directly — should use service
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        consumer.Subscribe("payment-events");   // VIOLATION E1: topic name wrong format

        while (!ct.IsCancellationRequested)
        {
            var result = consumer.Consume(ct);
            var message = result.Message.Value;

            try
            {
                // VIOLATION E3: no idempotency check — duplicate events = duplicate payments
                // VIOLATION E5: direct DB access in consumer
                var payment = await dbContext.Payments.FindAsync(message.PaymentId);

                if (payment == null)
                    return;

                // VIOLATION E5: business logic in consumer
                if (payment.Amount > 100_000)
                {
                    payment.Status = PaymentStatus.Pending;
                }
                else
                {
                    payment.Status = PaymentStatus.Completed;
                }

                await dbContext.SaveChangesAsync(ct);

                // VIOLATION E6: no audit logging
            }
            catch (Exception ex)
            {
                // VIOLATION E4: exception swallowed — no DLQ, message lost
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}

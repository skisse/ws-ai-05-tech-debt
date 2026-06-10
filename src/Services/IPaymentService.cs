using AcmeFinancial.Payments.Models;

namespace AcmeFinancial.Payments.Services;

public interface IPaymentService
{
    Task<Result<Payment>> GetPaymentAsync(Guid id);
    Task<Result<Payment>> CreatePaymentAsync(CreatePaymentRequest request, string userId);
    Task<Result<IEnumerable<Payment>>> GetPaymentsForAccountAsync(string accountNumber);
    Task<Result<Payment>> ProcessPaymentEventAsync(Guid paymentId, CancellationToken ct = default);
}

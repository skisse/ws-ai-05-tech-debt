using AcmeFinancial.Payments.Models;

namespace AcmeFinancial.Payments.Repositories;

public interface IPaymentRepository
{
    Task<Payment?> GetAsync(Guid id);
    Task<IEnumerable<Payment>> GetForAccountAsync(string accountNumber);
    Task<Payment> CreateAsync(Payment payment);
    Task<Payment> UpdateStatusAsync(Guid id, PaymentStatus newStatus);
}

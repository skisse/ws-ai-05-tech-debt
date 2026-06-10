using AcmeFinancial.Payments.Models;
using Microsoft.EntityFrameworkCore;

namespace AcmeFinancial.Payments.Repositories;

public class PaymentRepository(PaymentDbContext dbContext) : IPaymentRepository
{
    public async Task<Payment?> GetAsync(Guid id)
    {
        var entity = await dbContext.Payments.FindAsync(id);
        return entity?.ToDomainModel();
    }

    public async Task<IEnumerable<Payment>> GetForAccountAsync(string accountNumber)
    {
        return await dbContext.Payments
            .Where(p => p.FromAccountNumber == accountNumber)
            .Select(p => p.ToDomainModel())
            .ToListAsync();
    }

    public async Task<Payment> CreateAsync(Payment payment)
    {
        var entity = PaymentEntity.FromDomainModel(payment);
        dbContext.Payments.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity.ToDomainModel();
    }

    public async Task<Payment> UpdateStatusAsync(Guid id, PaymentStatus newStatus)
    {
        var entity = await dbContext.Payments.FindAsync(id)
            ?? throw new InvalidOperationException($"Payment {id} not found");
        entity.Status = newStatus.ToString();
        await dbContext.SaveChangesAsync();
        return entity.ToDomainModel();
    }
}

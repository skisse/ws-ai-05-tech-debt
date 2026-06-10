using AcmeFinancial.Payments.Models;

namespace AcmeFinancial.Payments.Services;

public interface IIdempotencyService
{
    Task<Result<Payment>?> GetCachedResultAsync(Guid key, CancellationToken ct = default);
    Task StoreResultAsync(Guid key, Result<Payment> result, CancellationToken ct = default);
}

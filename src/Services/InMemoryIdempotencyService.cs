using System.Collections.Concurrent;
using AcmeFinancial.Payments.Models;

namespace AcmeFinancial.Payments.Services;

public class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<Guid, Result<Payment>> _cache = new();

    public Task<Result<Payment>?> GetCachedResultAsync(Guid key, CancellationToken ct = default)
    {
        _cache.TryGetValue(key, out var result);
        return Task.FromResult(result);
    }

    public Task StoreResultAsync(Guid key, Result<Payment> result, CancellationToken ct = default)
    {
        _cache.TryAdd(key, result);
        return Task.CompletedTask;
    }
}

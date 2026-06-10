namespace AcmeFinancial.Payments;

public interface IAuditLogger
{
    Task LogAsync(string userId, string action, string resourceId, CancellationToken ct = default);
}

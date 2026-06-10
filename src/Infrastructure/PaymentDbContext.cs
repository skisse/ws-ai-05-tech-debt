using AcmeFinancial.Payments.Models;
using Microsoft.EntityFrameworkCore;

namespace AcmeFinancial.Payments.Models;

// EF entity — separate from the domain Payment record
public class PaymentEntity
{
    public Guid Id { get; set; }
    public string FromAccountNumber { get; set; } = "";
    public string ToAccountNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    // DEBT: Status stored as string instead of enum — magic string comparisons throughout the codebase
    public string Status { get; set; } = "Pending";

    public Payment ToDomainModel() => new(
        Id,
        FromAccountNumber,
        ToAccountNumber,
        Amount,
        Currency,
        CreatedAt,
        Enum.TryParse<PaymentStatus>(Status, out var s) ? s : PaymentStatus.Pending
    );

    public static PaymentEntity FromDomainModel(Payment p) => new()
    {
        Id = p.Id,
        FromAccountNumber = p.FromAccountNumber,
        ToAccountNumber = p.ToAccountNumber,
        Amount = p.Amount,
        Currency = p.Currency,
        CreatedAt = p.CreatedAt,
        Status = p.Status.ToString()
    };
}

public class AccountEntity
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public decimal Balance { get; set; }
}

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
}

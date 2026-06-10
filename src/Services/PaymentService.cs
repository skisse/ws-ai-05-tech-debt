// DEBT: God class — payment logic, fraud check, SMS notification, balance update,
//       daily limit enforcement, and status management all in one class.
//       Violates SRP (Single Responsibility Principle) and makes unit testing nearly impossible.

// DEBT: No interface — PaymentServiceManager cannot be mocked or substituted.
//       Controllers that use this class are untestable in isolation.
//       Violates arkitekturregler.md Rule 5 (Interface-First).

using System.Data;
using AcmeFinancial.Payments.Models;
using Microsoft.EntityFrameworkCore;

namespace AcmeFinancial.Payments.Services;

public class PaymentServiceManager
{
    // DEBT: Hardcoded URLs — must change code to point to different environments.
    //       Should be injected via IOptions<PaymentOptions> or IConfiguration.
    private const string FraudApiUrl = "https://fraud-api.internal.acme.no/api/v1/check";
    private const string SmsGatewayUrl = "https://sms.acme.no/send";

    // DEBT: Magic number — business rule encoded as a constant in the service.
    //       Daily limits belong in configuration or a dedicated policy service.
    private const decimal DailyLimit = 50000m;

    // DEBT: DbContext injected directly into service layer — violates layering rule.
    //       Services must use IPaymentRepository, never DbContext.
    //       Violates arkitekturregler.md Rule 1 (Strict Layering).
    private readonly PaymentDbContext _dbContext;

    // DEBT: HttpClient injected directly — not registered via IHttpClientFactory,
    //       causes socket exhaustion under load and makes HTTP calls untestable.
    private readonly HttpClient _httpClient;

    private readonly ILogger<PaymentServiceManager> _logger;

    // DEBT: No CancellationToken on constructor — cancellation cannot be propagated.
    public PaymentServiceManager(
        PaymentDbContext dbContext,
        HttpClient httpClient,
        ILogger<PaymentServiceManager> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _logger = logger;
    }

    // DEBT: Returns object instead of Result<T> — callers must cast and cannot distinguish
    //       success from failure without catching exceptions. Violates Rule 3 (Result<T>).
    // DEBT: Missing CancellationToken — request cannot be cancelled. Violates Rule 7.
    // DEBT: Missing audit log — reading customer payment data without an audit trail
    //       violates PSD2 Article 97 and DORA Article 9(1). Violates Rule 2.
    public async Task<object> GetPayment(Guid id)
    {
        // DEBT: String interpolation in log — not queryable in structured log systems (Seq, Kibana).
        //       Violates csharp-kodestandard.md logging rules.
        _logger.LogInformation($"Getting payment {id}");

        var entity = await _dbContext.Payments.FindAsync(id);

        if (entity == null)
        {
            // DEBT: Throws exception for a normal business case (not found).
            //       Controllers must wrap every call in try/catch. Violates Rule 3.
            throw new Exception("Payment not found");
        }

        // DEBT: Logs account number directly — GDPR Article 32 / PCI DSS Req 3 violation.
        //       Account numbers are PII and must never appear in logs.
        _logger.LogInformation("Payment {PaymentId} retrieved", entity.Id);

        return entity;
    }

    // DEBT: Returns object instead of Result<T>.
    // DEBT: Missing CancellationToken on async method. Violates Rule 7.
    // DEBT: No idempotency key support — duplicate POST requests will create duplicate payments.
    //       Violates Rule 10 (Idempotency Keys on Payment Endpoints).
    public async Task<object> CreatePayment(string fromAccount, string toAccount, decimal amount, string currency, string userId)
    {
        // DEBT: String interpolation in log.
        _logger.LogInformation("Payment creation started for user {UserId}", userId);

        // DEBT: Business logic (validation) mixed into god class instead of a validation layer.
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive");

        if (string.IsNullOrWhiteSpace(fromAccount) || string.IsNullOrWhiteSpace(toAccount))
            throw new ArgumentException("Account numbers required");

        // DEBT: Direct EF query in service — should go through IPaymentRepository.
        //       Violates Rule 1 (layering) — services must not touch DbContext.
        var accountEntity = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.AccountNumber == fromAccount);

        if (accountEntity == null)
            throw new Exception("Account not found");

        // DEBT: Business rule (balance check) in god class — belongs in a dedicated
        //       AccountDomainService or enforced at repository level.
        if (accountEntity.Balance < amount)
            throw new Exception("Insufficient funds");

        // DEBT: Daily limit check done inline — magic constant, no audit trail,
        //       no separate policy object. Cannot be configured per customer tier.
        var today = DateTime.Now.Date;  // DEBT: DateTime.Now instead of DateTimeOffset.UtcNow — timezone bug.
        var dailyTotal = await _dbContext.Payments
            .Where(p => p.FromAccountNumber == fromAccount
                     && p.CreatedAt >= today
                     && p.Status != "Cancelled")  // DEBT: Magic string instead of PaymentStatus enum.
            .SumAsync(p => p.Amount);

        if (dailyTotal + amount > DailyLimit)
            throw new Exception($"Daily limit of {DailyLimit} exceeded");

        // DEBT: Fraud check exception swallowed — if fraud API is down or returns error,
        //       the payment silently proceeds. This is a security regression that allows
        //       fraudulent payments to bypass controls. Should fail-closed, not fail-open.
        bool isFraudulent = false;
        try
        {
            var fraudResponse = await _httpClient.GetAsync($"{FraudApiUrl}?account={fromAccount}&amount={amount}");
            if (fraudResponse.IsSuccessStatusCode)
            {
                var body = await fraudResponse.Content.ReadAsStringAsync();
                isFraudulent = body.Contains("\"fraud\":true");
            }
        }
        catch (Exception ex)
        {
            // DEBT: Fraud check failure silently swallowed — payment proceeds even if fraud
            //       detection is unavailable. Must fail-closed (reject payment on error).
            _logger.LogWarning($"Fraud check failed: {ex.Message}. Proceeding anyway.");
        }

        if (isFraudulent)
            throw new Exception("Payment flagged as fraudulent");

        // DEBT: Magic string for status ("Pending") instead of PaymentStatus.Pending enum value.
        //       Breaks refactoring tools and causes runtime errors if string changes.
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            FromAccountNumber = fromAccount,
            ToAccountNumber = toAccount,
            Amount = amount,
            Currency = currency,
            CreatedAt = DateTime.Now,  // DEBT: DateTime.Now — not UTC, timezone-sensitive bug.
            Status = "Pending"         // DEBT: Magic string instead of PaymentStatus enum.
        };

        _dbContext.Payments.Add(payment);

        // DEBT: Balance mutation inside payment service — violates SRP.
        //       Balance updates belong in AccountService via outbox pattern (Rule E7).
        //       This creates a race condition: two concurrent payments can both see sufficient
        //       balance, both debit, resulting in overdraft.
        accountEntity.Balance -= amount;

        // DEBT: No outbox pattern — balance debit and payment creation committed in one
        //       EF SaveChanges, but no Kafka event emitted atomically.
        //       If the app crashes after SaveChanges, the event is never published.
        await _dbContext.SaveChangesAsync();

        // DEBT: Fire-and-forget Task.Run with swallowed exception — SMS notification failures
        //       are completely invisible. No retry, no dead-letter, no alerting.
        //       Unobserved task exceptions are swallowed by the runtime.
        _ = Task.Run(async () =>
        {
            try
            {
                await _httpClient.PostAsync(SmsGatewayUrl,
                    new StringContent($"{{\"to\":\"+47{fromAccount}\",\"msg\":\"Payment of {amount} {currency} initiated\"}}"));
            }
            catch
            {
                // DEBT: Exception completely swallowed — failure is invisible to operators.
                //       Use a background job framework (Hangfire, Quartz) or outbox instead.
            }
        });

        // DEBT: No audit log emitted after successful payment creation — PSD2/DORA violation.
        //       Violates Rule 2 (Audit Logging on All Customer Data Endpoints).

        return payment;
    }

    // DEBT: Returns object instead of Result<T>.
    // DEBT: Missing CancellationToken. Violates Rule 7.
    // DEBT: Missing audit log on list operation — reading customer data without audit trail.
    //       Violates PSD2/DORA requirements. Violates Rule 2.
    public async Task<object> GetPaymentsForAccount(string accountNumber)
    {
        _logger.LogInformation("Fetching payments for account");

        // DEBT: Direct EF query in service — violates layering Rule 1.
        var payments = await _dbContext.Payments
            .Where(p => p.FromAccountNumber == accountNumber)
            .ToListAsync();

        return payments;
    }

    // DEBT: Cancel method:
    //   - Returns object instead of Result<T>
    //   - Missing CancellationToken
    //   - Missing audit log on cancellation (PSD2/DORA)
    //   - Magic string for status ("Cancelled")
    //   - Direct EF in service layer (layering violation)
    public async Task<object> CancelPayment(Guid id, string userId)
    {
        _logger.LogInformation($"Cancelling payment {id}");

        // DEBT: Direct EF query in service — violates Rule 1 (layering).
        var entity = await _dbContext.Payments.FindAsync(id);

        if (entity == null)
            throw new Exception("Payment not found");

        // DEBT: No check whether payment is in a cancellable state — cancelling a
        //       Completed payment would corrupt financial records.
        // DEBT: Magic string "Cancelled" instead of PaymentStatus enum value.
        entity.Status = "Cancelled";

        await _dbContext.SaveChangesAsync();

        // DEBT: No audit log — who cancelled what payment and when is not recorded.
        //       Financial cancellation without audit trail is a PSD2/DORA violation.
        //       Violates Rule 2.

        return entity;
    }

    // DEBT: GetDailyReport method — a reporting concern embedded in the payment service god class.
    //       Should be in a dedicated ReportingService. No interface, no testability.
    // DEBT: Returns DataTable — not a domain object, cannot be used with Result<T>.
    // DEBT: Missing CancellationToken. Violates Rule 7.
    public async Task<DataTable> GetDailyReport(DateTime date)
    {
        // DEBT: DateTime parameter instead of DateTimeOffset — timezone-sensitive.
        _logger.LogInformation($"Generating daily report for {date:yyyy-MM-dd}");

        // DEBT: Direct EF in service — violates Rule 1.
        var payments = await _dbContext.Payments
            .Where(p => p.CreatedAt.Date == date.Date)
            .ToListAsync();

        // DEBT: DataTable returned — not a domain object. Mixes data access and formatting.
        var table = new DataTable("DailyReport");
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("From", typeof(string));
        table.Columns.Add("To", typeof(string));
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("Status", typeof(string));

        foreach (var p in payments)
            table.Rows.Add(p.Id, p.FromAccountNumber, p.ToAccountNumber, p.Amount, p.Status);

        return table;
    }
}

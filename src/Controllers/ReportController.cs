// DEBT: Fat controller — each action method is 80+ lines.
//       Business logic, data access, formatting, and HTTP concerns all in one place.
//       Violates arkitekturregler.md Rule 9 (Controllers Are Thin — Max 20 Lines per Action).

// DEBT: Controller accesses DbContext directly — violates Rule 1 (Strict Layering).
//       All data access must go through Service → Repository → Database.

// DEBT: No audit logging on any endpoint — reads and writes of customer financial data
//       without audit trail. Violates PSD2 Article 97 / DORA Article 9(1). Violates Rule 2.

using System.Data;
using System.Net.Http;
using System.Text.Json;
using AcmeFinancial.Payments.Models;
using AcmeFinancial.Payments.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcmeFinancial.Payments.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    // DEBT: DbContext injected into controller — hard layering violation. Rule 1.
    private readonly PaymentDbContext _dbContext;

    // DEBT: HttpClient injected directly — not via IHttpClientFactory.
    //       Socket exhaustion risk. Not mockable for testing. Violates Rule 4.
    private readonly HttpClient _httpClient;

    // DEBT: ReportingHelper injected by concrete class — no interface, not substitutable.
    //       Violates Rule 5 (Interface-First). Untestable.
    private readonly ReportingHelper _reportingHelper;

    private readonly ILogger<ReportController> _logger;
    private readonly IAuditLogger _auditLogger;

    private string UserId => User.FindFirst("sub")?.Value ?? "unknown";

    public ReportController(
        PaymentDbContext dbContext,
        HttpClient httpClient,
        ReportingHelper reportingHelper,
        ILogger<ReportController> logger,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _reportingHelper = reportingHelper;
        _logger = logger;
        _auditLogger = auditLogger;
    }

    // DEBT: 90-line action method — violates Rule 9 (max 20 lines per action).
    // DEBT: Business logic in controller — filtering, aggregation, thresholds.
    // DEBT: No audit log — reading customer financial data without audit trail.
    // DEBT: No CancellationToken parameter. Violates Rule 7.
    // DEBT: No Result<T> — throws exceptions on errors. Violates Rule 3.
    [HttpGet("daily")]
    public async Task<IActionResult> GetDailyReport([FromQuery] string? date = null)
    {
        // DEBT: String interpolation in log — not queryable. Violates logging standard.
        _logger.LogInformation("Daily report requested for date {ReportDate}", date);

        DateTime reportDate;

        // DEBT: Date parsing with try/catch for flow control — anti-pattern.
        //       Use DateTime.TryParse instead.
        try
        {
            reportDate = date != null ? DateTime.Parse(date) : DateTime.Now.Date;
        }
        catch
        {
            return BadRequest("Invalid date format");
        }

        // DEBT: Business logic in controller — date range validation, weekend check, etc.
        if (reportDate > DateTime.Now)
        {
            return BadRequest("Cannot generate report for future dates");
        }

        if (reportDate < DateTime.Now.AddYears(-1))
        {
            // DEBT: Business rule (1-year limit) hardcoded in controller — belongs in service.
            return BadRequest("Reports only available for the last 12 months");
        }

        // DEBT: Direct EF query in controller — violates Rule 1.
        //       Should call a ReportingService.GetDailyReportAsync(date).
        var payments = await _dbContext.Payments
            .Where(p => p.CreatedAt.Date == reportDate.Date)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        // DEBT: Inline aggregation — business calculation in controller.
        var totalVolume = payments.Where(p => p.Status != "Cancelled").Sum(p => p.Amount);
        var avgAmount = payments.Any() ? payments.Average(p => p.Amount) : 0;
        var pendingCount = payments.Count(p => p.Status == "Pending");
        var completedCount = payments.Count(p => p.Status == "Completed");
        var cancelledCount = payments.Count(p => p.Status == "Cancelled");
        var highValuePayments = payments.Where(p => p.Amount > 10000).ToList();  // DEBT: Magic number.

        // DEBT: External HTTP call inside controller action — network latency blocks the
        //       request thread. Not resilient (no timeout, no retry, no circuit breaker).
        //       Should be encapsulated in a dedicated ExchangeRateService.
        decimal eurRate = 1m;
        try
        {
            var rateResponse = await _httpClient.GetAsync("https://api.exchangerate.internal.acme.no/NOK/EUR");
            if (rateResponse.IsSuccessStatusCode)
            {
                var rateJson = await rateResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(rateJson);
                eurRate = doc.RootElement.GetProperty("rate").GetDecimal();
            }
        }
        catch
        {
            // DEBT: Exception swallowed — EUR conversion silently falls back to 1:1
            //       without any log or notification. Report will show wrong EUR amounts.
            _logger.LogWarning("Failed to fetch EUR rate — using 1:1 fallback");
        }

        // DEBT: Manual DTO construction in controller — should be a dedicated mapper or record.
        //       Leaks EF entity shape to HTTP response (Rule 8 — entity leakage).
        var responsePayments = payments.Select(p => new
        {
            p.Id,
            p.FromAccountNumber,   // DEBT: PII (account numbers) returned in response without masking.
            p.ToAccountNumber,     // DEBT: PII in response.
            p.Amount,
            AmountEur = Math.Round(p.Amount * eurRate, 2),
            p.Currency,
            p.Status,
            p.CreatedAt,
            IsHighValue = p.Amount > 10000  // DEBT: Magic number duplicated from above.
        }).ToList();

        _logger.LogInformation("Daily report: {PaymentCount} payments, high value count: {HighValueCount}",
            payments.Count, highValuePayments.Count);

        await _auditLogger.LogAsync(UserId, "REPORT_DAILY_READ", reportDate.ToString("yyyy-MM-dd"));

        return Ok(new
        {
            Date = reportDate.ToString("yyyy-MM-dd"),
            TotalCount = payments.Count,
            TotalVolume = totalVolume,
            TotalVolumeEur = Math.Round(totalVolume * eurRate, 2),
            AverageAmount = Math.Round(avgAmount, 2),
            PendingCount = pendingCount,
            CompletedCount = completedCount,
            CancelledCount = cancelledCount,
            HighValueCount = highValuePayments.Count,
            Payments = responsePayments
        });
    }

    // DEBT: 85-line action method. Violates Rule 9.
    // DEBT: Business logic in controller.
    // DEBT: Direct DB access in controller. Violates Rule 1.
    // DEBT: No audit log. Violates Rule 2 (PSD2/DORA).
    // DEBT: No Result<T>. Violates Rule 3.
    // DEBT: No CancellationToken. Violates Rule 7.
    [HttpGet("account/{accountNumber}/summary")]
    public async Task<IActionResult> GetAccountSummary(string accountNumber, [FromQuery] int months = 3)
    {
        // DEBT: String interpolation in log.
        _logger.LogInformation("Account summary requested, last {Months} months", months);

        // DEBT: Input validation in controller instead of service/validator.
        if (string.IsNullOrWhiteSpace(accountNumber))
            return BadRequest("Account number required");

        // DEBT: Business rule inline in controller — months capped at 12 without explanation.
        if (months < 1 || months > 12)
            return BadRequest("Months must be between 1 and 12");

        // DEBT: DateTime.Now — timezone bug. Use DateTimeOffset.UtcNow.
        var from = DateTime.Now.AddMonths(-months);
        var to = DateTime.Now;

        // DEBT: Direct EF queries in controller — multiple queries that belong in a repository.
        var outgoing = await _dbContext.Payments
            .Where(p => p.FromAccountNumber == accountNumber
                     && p.CreatedAt >= from
                     && p.CreatedAt <= to)
            .ToListAsync();

        var incoming = await _dbContext.Payments
            .Where(p => p.ToAccountNumber == accountNumber
                     && p.CreatedAt >= from
                     && p.CreatedAt <= to)
            .ToListAsync();

        // DEBT: Inline business calculation — should be in AccountSummaryService.
        var totalSent = outgoing.Where(p => p.Status != "Cancelled").Sum(p => p.Amount);
        var totalReceived = incoming.Where(p => p.Status == "Completed").Sum(p => p.Amount);
        var netPosition = totalReceived - totalSent;

        // DEBT: Inline frequency analysis — complex business logic in controller.
        var mostFrequentRecipient = outgoing
            .Where(p => p.Status != "Cancelled")
            .GroupBy(p => p.ToAccountNumber)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "N/A";

        var largestSinglePayment = outgoing.Any()
            ? outgoing.Max(p => p.Amount)
            : 0m;

        // DEBT: Inline fraud heuristic in controller action — complex decision logic
        //       that should be in a FraudRiskService.
        var suspiciousPayments = outgoing
            .Where(p => p.Amount > 25000 ||
                        (p.Currency != "NOK" && p.Amount > 5000) ||
                        p.ToAccountNumber.StartsWith("99"))  // DEBT: Magic prefix check.
            .ToList();

        bool accountFlaggedForReview = suspiciousPayments.Count >= 3 ||
                                        totalSent > 100000;  // DEBT: Magic threshold.

        // DEBT: External HTTP call in controller to fetch account holder name.
        //       Should be in an AccountInfoService with proper error handling and caching.
        string accountHolderName = "Unknown";
        try
        {
            var profileUrl = $"https://accounts.internal.acme.no/api/accounts/{accountNumber}/profile";
            var profileResponse = await _httpClient.GetAsync(profileUrl);
            if (profileResponse.IsSuccessStatusCode)
            {
                var profileJson = await profileResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(profileJson);
                accountHolderName = doc.RootElement.GetProperty("name").GetString() ?? "Unknown";
            }
        }
        catch
        {
            _logger.LogWarning("Could not fetch account holder name");
        }

        _logger.LogInformation("Account summary generated, flagged: {Flagged}", accountFlaggedForReview);

        await _auditLogger.LogAsync(UserId, "REPORT_ACCOUNT_SUMMARY_READ", accountNumber);

        return Ok(new
        {
            AccountNumber = accountNumber,  // DEBT: PII in response without masking.
            AccountHolder = accountHolderName,
            PeriodMonths = months,
            From = from.ToString("yyyy-MM-dd"),
            To = to.ToString("yyyy-MM-dd"),
            TotalSent = totalSent,
            TotalReceived = totalReceived,
            NetPosition = netPosition,
            OutgoingCount = outgoing.Count,
            IncomingCount = incoming.Count,
            LargestSinglePayment = largestSinglePayment,
            MostFrequentRecipient = mostFrequentRecipient,
            SuspiciousPaymentCount = suspiciousPayments.Count,
            FlaggedForReview = accountFlaggedForReview
        });
    }

    // DEBT: 80-line action method. Violates Rule 9.
    // DEBT: No audit log on export of customer financial data. Major PSD2/DORA violation.
    // DEBT: Direct DB access. Violates Rule 1.
    // DEBT: No CancellationToken. Violates Rule 7.
    [HttpPost("export")]
    public async Task<IActionResult> ExportPayments([FromBody] ExportRequest request)
    {
        _logger.LogInformation("Export requested: {Format}", request?.Format);

        if (request == null)
            return BadRequest("Request body required");

        if (string.IsNullOrWhiteSpace(request.AccountNumber))
            return BadRequest("Account number required");

        // DEBT: Business rule inline — format validation belongs in a validator class.
        if (request.Format != "csv" && request.Format != "json")
            return BadRequest("Format must be csv or json");

        // DEBT: Date range defaulting logic in controller — belongs in service or request validator.
        // DEBT: DateTime.Now — timezone bug.
        var from = request.From ?? DateTime.Now.AddMonths(-1);
        var to = request.To ?? DateTime.Now;

        if (from > to)
            return BadRequest("From date must be before To date");

        // DEBT: Direct EF query — violates Rule 1.
        var payments = await _dbContext.Payments
            .Where(p => p.FromAccountNumber == request.AccountNumber
                     && p.CreatedAt >= from
                     && p.CreatedAt <= to)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        // DEBT: Inline file formatting in controller action — should be in an ExportService
        //       or separate formatter classes (CsvPaymentFormatter, JsonPaymentFormatter).
        if (request.Format == "csv")
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("PaymentId,FromAccount,ToAccount,Amount,Currency,Status,CreatedAt");

            foreach (var p in payments)
            {
                // DEBT: Raw account numbers in CSV export — PII without any access control check.
                //       Is the caller authorised to export this account's data?
                csv.AppendLine($"{p.Id},{p.FromAccountNumber},{p.ToAccountNumber}," +
                               $"{p.Amount},{p.Currency},{p.Status},{p.CreatedAt:o}");
            }

            await _auditLogger.LogAsync(UserId, "REPORT_EXPORT", request.AccountNumber);
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()),
                "text/csv",
                $"payments-{request.AccountNumber}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
        }
        else
        {
            // DEBT: Returns raw DB entity shape in JSON — EF entity leakage. Violates Rule 8.
            //       Should map to a PaymentExportDto before serialisation.
            await _auditLogger.LogAsync(UserId, "REPORT_EXPORT", request.AccountNumber);
            return Ok(payments);
        }
    }
}

// DEBT: Request model defined in the controller file — should be in Models/ folder.
// DEBT: Uses DateTime instead of DateTimeOffset — timezone bug.
public record ExportRequest(
    string AccountNumber,
    string Format,
    DateTime? From,
    DateTime? To
);

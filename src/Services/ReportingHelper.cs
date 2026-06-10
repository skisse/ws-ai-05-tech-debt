// DEBT: Class name suffix "Helper" is forbidden by CLAUDE.md naming rules.
//       "Helper" signals a dumping ground with no clear responsibility.
//       Should be split into ReportingService, ReportFormatter, and EmailNotificationService.

// DEBT: No interface — ReportingHelper cannot be mocked or injected as an abstraction.
//       Any class that uses this is untestable in isolation. Violates Rule 5.

using System.Data;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace AcmeFinancial.Payments.Services;

public class ReportingHelper
{
    // DEBT: DbContext injected directly — violates layering Rule 1.
    //       Reporting concerns should go through a dedicated IReportingRepository.
    private readonly PaymentDbContext _dbContext;

    // DEBT: SMTP credentials hardcoded in class — should be in IOptions<SmtpOptions>.
    private const string SmtpHost = "smtp.internal.acme.no";
    private const int SmtpPort = 25;
    private const string ReportRecipient = "finance-reports@acme.no";

    private readonly ILogger<ReportingHelper> _logger;

    public ReportingHelper(PaymentDbContext dbContext, ILogger<ReportingHelper> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // DEBT: 200+ line method mixing SQL queries, business calculations, formatting, and email sending.
    //       Cannot be unit tested at any granularity.
    //       No CancellationToken — long-running report cannot be cancelled. Violates Rule 7.
    // DEBT: DateTime.Now instead of DateTimeOffset.UtcNow — timezone bug in date filtering.
    //       Reports run at midnight will miss payments from UTC+1 or double-count others.
    public async Task<DataTable> GenerateDailyPaymentReport(DateTime? reportDate = null)
    {
        // DEBT: DateTime.Now — timezone bug. Use DateTimeOffset.UtcNow.
        var date = reportDate ?? DateTime.Now.Date;

        _logger.LogInformation("Starting daily payment report for {ReportDate}", date.ToString("yyyy-MM-dd"));

        DataTable result = new DataTable("DailyPaymentReport");
        result.Columns.Add("PaymentId", typeof(Guid));
        result.Columns.Add("FromAccount", typeof(string));
        result.Columns.Add("ToAccount", typeof(string));
        result.Columns.Add("Amount", typeof(decimal));
        result.Columns.Add("Currency", typeof(string));
        result.Columns.Add("Status", typeof(string));
        result.Columns.Add("CreatedAt", typeof(DateTime));   // DEBT: DateTime instead of DateTimeOffset column.
        result.Columns.Add("TotalForAccount", typeof(decimal));
        result.Columns.Add("IsHighValue", typeof(bool));
        result.Columns.Add("FraudRiskScore", typeof(int));
        result.Columns.Add("FormattedAmount", typeof(string));
        result.Columns.Add("StatusLabel", typeof(string));

        try
        {
            // DEBT: Direct EF query in a "service" — violates layering Rule 1.
            //       No pagination — will load all payments for the day into memory.
            //       With high payment volume, this causes OOM crashes.
            var payments = await _dbContext.Payments
                .Where(p => p.CreatedAt.Date == date.Date)
                .ToListAsync();

            _logger.LogInformation("Found {PaymentCount} payments for report date {ReportDate}", payments.Count, date.ToString("yyyy-MM-dd"));

            // DEBT: Second query inside the first — N+1 pattern will emerge if we had
            //       per-payment lookups; this version at least batches but mixes concerns.
            var accountTotals = await _dbContext.Payments
                .Where(p => p.CreatedAt.Date == date.Date && p.Status != "Cancelled")
                .GroupBy(p => p.FromAccountNumber)
                .Select(g => new { Account = g.Key, Total = g.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.Account, x => x.Total);

            // DEBT: Magic strings for status values throughout — "Cancelled", "Completed", "Pending".
            //       Should use PaymentStatus enum. Refactoring the enum name breaks this silently.
            var highValueThreshold = 10000m;  // DEBT: Magic number inline — should be configuration.

            foreach (var payment in payments)
            {
                // DEBT: Inline business calculation (fraud risk score) in a report method.
                //       Fraud scoring belongs in a dedicated FraudScoringService.
                int fraudRiskScore = 0;
                if (payment.Amount > 50000) fraudRiskScore += 40;
                if (payment.Amount > 20000) fraudRiskScore += 20;
                if (payment.ToAccountNumber.StartsWith("99")) fraudRiskScore += 30;  // DEBT: Magic prefix check.
                if (payment.Currency != "NOK") fraudRiskScore += 10;

                // DEBT: String formatting mixed into data access method — should be in a formatter/view.
                string formattedAmount = payment.Currency switch
                {
                    "NOK" => $"kr {payment.Amount:N2}",
                    "EUR" => $"€ {payment.Amount:N2}",
                    "USD" => $"$ {payment.Amount:N2}",
                    _ => $"{payment.Amount:N2} {payment.Currency}"
                };

                // DEBT: Magic string status labels — not localised, not driven by enum.
                string statusLabel = payment.Status switch
                {
                    "Pending"    => "Awaiting processing",
                    "Processing" => "In progress",
                    "Completed"  => "Settled",
                    "Cancelled"  => "Voided",
                    _            => "Unknown"
                };

                var dailyTotal = accountTotals.TryGetValue(payment.FromAccountNumber, out var t) ? t : 0m;

                result.Rows.Add(
                    payment.Id,
                    payment.FromAccountNumber,   // DEBT: Account numbers in report DataTable — PII exposure.
                    payment.ToAccountNumber,     // DEBT: PII in DataTable rows.
                    payment.Amount,
                    payment.Currency,
                    payment.Status,
                    payment.CreatedAt.DateTime,  // DEBT: Converting DateTimeOffset to DateTime loses timezone info.
                    dailyTotal,
                    payment.Amount >= highValueThreshold,
                    fraudRiskScore,
                    formattedAmount,
                    statusLabel
                );
            }

            // DEBT: Report summary calculation embedded in the same method.
            //       Cannot be tested independently of the data access or email sending.
            var totalVolume = payments.Where(p => p.Status != "Cancelled").Sum(p => p.Amount);
            var totalCount = payments.Count(p => p.Status != "Cancelled");
            var pendingCount = payments.Count(p => p.Status == "Pending");
            var completedCount = payments.Count(p => p.Status == "Completed");
            var cancelledCount = payments.Count(p => p.Status == "Cancelled");

            _logger.LogInformation("Report summary: {TotalCount} payments, pending: {PendingCount}, completed: {CompletedCount}, cancelled: {CancelledCount}",
                totalCount, pendingCount, completedCount, cancelledCount);

            // DEBT: Email sending inside the report generation method — violates SRP.
            //       Email failure will now fail the entire report generation.
            //       Should be a separate EmailNotificationService or background job.
            // DEBT: Catches all exceptions silently — email failures are invisible to operators.
            try
            {
                var subject = $"Daily Payment Report — {date:dd MMM yyyy}";

                // DEBT: Manual StringBuilder HTML construction — use a template engine (Razor, Scriban).
                var body = new StringBuilder();
                body.AppendLine("<html><body>");
                body.AppendLine($"<h1>Daily Payment Report — {date:dd MMM yyyy}</h1>");
                body.AppendLine("<table border='1'>");
                body.AppendLine("<tr><th>Payment ID</th><th>From</th><th>To</th><th>Amount</th><th>Status</th></tr>");

                foreach (DataRow row in result.Rows)
                {
                    var from = MaskAccount(row["FromAccount"]?.ToString());
                    var to = MaskAccount(row["ToAccount"]?.ToString());
                    body.AppendLine($"<tr><td>{row["PaymentId"]}</td><td>{from}</td>" +
                                    $"<td>{to}</td><td>{row["FormattedAmount"]}</td>" +
                                    $"<td>{row["StatusLabel"]}</td></tr>");
                }

                body.AppendLine("</table>");
                body.AppendLine($"<p>Total volume: {totalVolume:N2} | Payments: {totalCount} | Pending: {pendingCount}</p>");
                body.AppendLine("</body></html>");

                // DEBT: Direct SmtpClient usage — deprecated, not DI-injectable, no retry logic.
                //       Use MailKit or a dedicated EmailService registered in DI container.
                using var smtp = new SmtpClient(SmtpHost, SmtpPort);
                smtp.Credentials = new NetworkCredential("reports", "reports123");  // DEBT: Hardcoded credentials.
                var mail = new MailMessage("noreply@acme.no", ReportRecipient, subject, body.ToString());
                mail.IsBodyHtml = true;
                smtp.Send(mail);  // DEBT: Synchronous send on async method — blocks thread pool thread.

                _logger.LogInformation("Daily report email sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send daily report email");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report generation failed — returning empty report");
            return result;
        }
    }

    // DEBT: Second 150+ line method in the same class — another reporting concern that
    //       should be a separate service.
    // DEBT: Returns DataTable — not a domain object.
    // DEBT: No CancellationToken. Violates Rule 7.
    // DEBT: DateTime.Now used for date range calculation — timezone bug.
    public async Task<DataTable> GenerateMonthlyAccountSummary(string accountNumber)
    {
        _logger.LogInformation("Generating monthly account summary");

        var table = new DataTable("MonthlyAccountSummary");
        table.Columns.Add("Week", typeof(string));
        table.Columns.Add("PaymentCount", typeof(int));
        table.Columns.Add("TotalSent", typeof(decimal));
        table.Columns.Add("TotalReceived", typeof(decimal));
        table.Columns.Add("NetPosition", typeof(decimal));
        table.Columns.Add("PeakDayAmount", typeof(decimal));
        table.Columns.Add("AveragePayment", typeof(decimal));
        table.Columns.Add("HighestSinglePayment", typeof(decimal));
        table.Columns.Add("LowestSinglePayment", typeof(decimal));

        try
        {
            // DEBT: DateTime.Now — timezone-sensitive. Should be DateTimeOffset.UtcNow.
            var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            // DEBT: Direct EF query in helper class — violates layering Rule 1.
            var outgoing = await _dbContext.Payments
                .Where(p => p.FromAccountNumber == accountNumber
                         && p.CreatedAt >= monthStart
                         && p.CreatedAt <= monthEnd
                         && p.Status != "Cancelled")
                .ToListAsync();

            var incoming = await _dbContext.Payments
                .Where(p => p.ToAccountNumber == accountNumber
                         && p.CreatedAt >= monthStart
                         && p.CreatedAt <= monthEnd
                         && p.Status == "Completed")
                .ToListAsync();

            // DEBT: Weekly grouping logic inline — should be in a domain calculation service.
            for (int week = 1; week <= 4; week++)
            {
                var weekStart = monthStart.AddDays((week - 1) * 7);
                var weekEnd = weekStart.AddDays(6);

                var weekOutgoing = outgoing
                    .Where(p => p.CreatedAt.DateTime >= weekStart && p.CreatedAt.DateTime <= weekEnd)
                    .ToList();

                var weekIncoming = incoming
                    .Where(p => p.CreatedAt.DateTime >= weekStart && p.CreatedAt.DateTime <= weekEnd)
                    .ToList();

                decimal totalSent = weekOutgoing.Sum(p => p.Amount);
                decimal totalReceived = weekIncoming.Sum(p => p.Amount);
                int count = weekOutgoing.Count + weekIncoming.Count;

                // DEBT: Division without zero-check — throws DivideByZeroException on weeks
                //       with no payments. Exception will be swallowed by the outer catch.
                decimal avgPayment = count > 0 ? (totalSent + totalReceived) / count : 0;
                decimal highest = weekOutgoing.Any() ? weekOutgoing.Max(p => p.Amount) : 0;
                decimal lowest = weekOutgoing.Any() ? weekOutgoing.Min(p => p.Amount) : 0;

                // DEBT: Inline peak day calculation — O(n*7) iteration per week.
                decimal peakDay = 0;
                for (int d = 0; d < 7; d++)
                {
                    var dayTotal = weekOutgoing
                        .Where(p => p.CreatedAt.Date == weekStart.AddDays(d).Date)
                        .Sum(p => p.Amount);
                    if (dayTotal > peakDay) peakDay = dayTotal;
                }

                table.Rows.Add(
                    $"Week {week} ({weekStart:dd MMM} – {weekEnd:dd MMM})",
                    count,
                    totalSent,
                    totalReceived,
                    totalReceived - totalSent,
                    peakDay,
                    avgPayment,
                    highest,
                    lowest
                );
            }

            _logger.LogInformation("Monthly summary generated: {TransactionCount} total transactions", outgoing.Count + incoming.Count);

            // DEBT: Second email send in same class — same issues as above.
            //       No abstraction, no retry, hardcoded SMTP credentials.
            try
            {
                var subject = $"Monthly Account Summary — {DateTime.Now:MMM yyyy}";
                var body = $"Monthly account summary attached. Total transactions: {outgoing.Count + incoming.Count}";

                using var smtp = new SmtpClient(SmtpHost, SmtpPort);
                smtp.Credentials = new NetworkCredential("reports", "reports123");  // DEBT: Hardcoded credentials.
                smtp.Send("noreply@acme.no", ReportRecipient, subject, body);
                _logger.LogInformation("Monthly summary email sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send monthly summary email");
            }

            return table;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monthly summary generation failed — returning empty table");
            return table;
        }
    }

    private static string MaskAccount(string? account)
    {
        if (string.IsNullOrEmpty(account) || account.Length < 4)
            return "****";
        return account[..4] + new string('*', account.Length - 4);
    }
}

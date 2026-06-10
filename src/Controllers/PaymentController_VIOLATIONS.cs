// ╔══════════════════════════════════════════════════════════╗
// ║  DEMO FILE: Contains deliberate architecture violations  ║
// ║  Used in workshop to show what Claude blocks             ║
// ╚══════════════════════════════════════════════════════════╝

using AcmeFinancial.Payments.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcmeFinancial.Payments.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController(
    PaymentDbContext _dbContext   // VIOLATION #1: DbContext injected directly into controller
) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPayment(Guid id)
    {
        // VIOLATION #2: Controller calls database directly — no service, no repository
        var payment = await _dbContext.Payments.FindAsync(id);

        if (payment == null)
            return NotFound();

        // VIOLATION #3: Missing audit logging (PSD2 requirement)
        return Ok(payment);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        // VIOLATION #4: Business logic in controller
        if (request.Amount <= 0)
            return BadRequest("Amount must be positive");

        // VIOLATION #5: Direct SQL via EF in controller
        var fromAccount = await _dbContext.Accounts
            .Where(a => a.AccountNumber == request.FromAccountNumber)
            .FirstOrDefaultAsync();

        if (fromAccount == null)
            return BadRequest("Account not found");

        if (fromAccount.Balance < request.Amount)
            return BadRequest("Insufficient funds");

        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            FromAccountNumber = request.FromAccountNumber,
            ToAccountNumber = request.ToAccountNumber,
            Amount = request.Amount,
            Currency = request.Currency,
            CreatedAt = DateTimeOffset.UtcNow,
            Status = PaymentStatus.Pending
        };

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        // VIOLATION #6: No audit logging before return
        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
    }
}

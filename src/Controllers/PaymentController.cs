using AcmeFinancial.Payments.Models;
using AcmeFinancial.Payments.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcmeFinancial.Payments.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController(
    IPaymentService paymentService,
    IIdempotencyService idempotencyService,
    IAuditLogger auditLogger
) : ControllerBase
{
    private string UserId => User.FindFirst("sub")?.Value ?? "unknown";

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPayment(Guid id)
    {
        var result = await paymentService.GetPaymentAsync(id);

        if (!result.Success)
            return NotFound(result.Error);

        await auditLogger.LogAsync(UserId, "PAYMENT_RETRIEVED", id.ToString());
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment(
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey,
        [FromBody] CreatePaymentRequest request)
    {
        if (idempotencyKey.HasValue)
        {
            var cached = await idempotencyService.GetCachedResultAsync(idempotencyKey.Value);
            if (cached != null)
            {
                if (cached.Value is null)
                    return StatusCode(500, "Cached idempotency result is invalid");
                await auditLogger.LogAsync(UserId, "PAYMENT_IDEMPOTENT_HIT", idempotencyKey.Value.ToString());
                return Ok(cached.Value);
            }
        }

        var result = await paymentService.CreatePaymentAsync(request, UserId);

        if (!result.Success)
            return BadRequest(result.Error);

        if (idempotencyKey.HasValue)
            await idempotencyService.StoreResultAsync(idempotencyKey.Value, result);

        await auditLogger.LogAsync(UserId, "PAYMENT_CREATED", result.Value!.Id.ToString());
        return CreatedAtAction(nameof(GetPayment), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("account/{accountNumber}")]
    public async Task<IActionResult> GetPaymentsForAccount(string accountNumber)
    {
        var result = await paymentService.GetPaymentsForAccountAsync(accountNumber);

        if (!result.Success)
            return NotFound(result.Error);

        await auditLogger.LogAsync(UserId, "PAYMENTS_LISTED", accountNumber);
        return Ok(result.Value);
    }
}

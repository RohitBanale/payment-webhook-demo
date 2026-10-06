using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentWebhookDemo.Api.Data;

namespace PaymentWebhookDemo.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await db.Payments.AsNoTracking().OrderByDescending(p => p.UpdatedAtUtc).Take(100).ToListAsync());

    [HttpGet("{paymentId}")]
    public async Task<IActionResult> Get(string paymentId)
    {
        var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentId == paymentId);
        return p is null ? NotFound() : Ok(p);
    }
}

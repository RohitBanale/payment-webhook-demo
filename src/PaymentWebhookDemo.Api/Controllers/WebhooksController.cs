using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentWebhookDemo.Api.Data;
using PaymentWebhookDemo.Api.Models;
using PaymentWebhookDemo.Api.Services;

namespace PaymentWebhookDemo.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhooksController(AppDbContext db, IConfiguration config, ILogger<WebhooksController> log) : ControllerBase
{
    /// Expected body: {"id":"evt_1","type":"payment.succeeded","data":{"paymentId":"pay_1","amount":49900,"currency":"INR"}}
    /// Header: X-Signature = hex(HMAC-SHA256(secret, rawBody))
    [HttpPost("payments")]
    public async Task<IActionResult> Receive()
    {
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync();

        var secret = config["Webhook:Secret"] ?? "";
        if (!WebhookSignature.IsValid(secret, body, Request.Headers["X-Signature"].FirstOrDefault()))
        {
            log.LogWarning("Webhook rejected: invalid signature");
            return Unauthorized();
        }

        JsonElement root;
        try { root = JsonDocument.Parse(body).RootElement; }
        catch (JsonException) { return BadRequest("Invalid JSON"); }

        if (!root.TryGetProperty("id", out var idEl) || !root.TryGetProperty("type", out var typeEl)
            || !root.TryGetProperty("data", out var data)
            || !data.TryGetProperty("paymentId", out var payIdEl)
            || !data.TryGetProperty("amount", out var amountEl) || !amountEl.TryGetInt64(out var amount)
            || !data.TryGetProperty("currency", out var curEl))
            return BadRequest("Missing required fields");

        var eventId = idEl.GetString() ?? "";
        var type = typeEl.GetString() ?? "";
        var status = type switch
        {
            "payment.succeeded" => "succeeded",
            "payment.failed" => "failed",
            "payment.refunded" => "refunded",
            _ => null
        };
        if (status is null) return Ok(new { ignored = type });   // unknown event types are acknowledged, not retried

        // Idempotency: providers retry, so the same event id must only be applied once.
        if (await db.WebhookEvents.AnyAsync(e => e.EventId == eventId))
            return Ok(new { duplicate = true });

        var paymentId = payIdEl.GetString() ?? "";
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        if (payment is null)
        {
            payment = new Payment { PaymentId = paymentId };
            db.Payments.Add(payment);
        }
        payment.Amount = amount;
        payment.Currency = curEl.GetString() ?? "";
        payment.Status = status;
        payment.UpdatedAtUtc = DateTime.UtcNow;

        db.WebhookEvents.Add(new WebhookEvent { EventId = eventId, Type = type, ReceivedAtUtc = DateTime.UtcNow });

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)   // concurrent duplicate delivery hit the unique index
        {
            return Ok(new { duplicate = true });
        }
        return Ok(new { processed = true });
    }
}

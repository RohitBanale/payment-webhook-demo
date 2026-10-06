namespace PaymentWebhookDemo.Api.Models;

public class Payment
{
    public int Id { get; set; }
    public string PaymentId { get; set; } = "";
    public long Amount { get; set; }          // minor units (e.g. paise / cents)
    public string Currency { get; set; } = "";
    public string Status { get; set; } = "";  // succeeded | failed | refunded
    public DateTime UpdatedAtUtc { get; set; }
}

public class WebhookEvent
{
    public int Id { get; set; }
    public string EventId { get; set; } = ""; // provider's unique event id -> idempotency key
    public string Type { get; set; } = "";
    public DateTime ReceivedAtUtc { get; set; }
}

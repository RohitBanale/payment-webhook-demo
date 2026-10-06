using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PaymentWebhookDemo.Api.Services;
using Xunit;

namespace PaymentWebhookDemo.Tests;

public class SignatureTests
{
    [Fact]
    public void Valid_signature_is_accepted() =>
        Assert.True(WebhookSignature.IsValid("s", "{}", WebhookSignature.Compute("s", "{}")));

    [Fact]
    public void Wrong_secret_or_payload_is_rejected()
    {
        var sig = WebhookSignature.Compute("s", "{}");
        Assert.False(WebhookSignature.IsValid("other", "{}", sig));
        Assert.False(WebhookSignature.IsValid("s", "{ }", sig));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Missing_or_malformed_signature_is_rejected(string? sig) =>
        Assert.False(WebhookSignature.IsValid("s", "{}", sig));
}

public class WebhookEndpointTests : IClassFixture<WebhookEndpointTests.Factory>
{
    private const string Secret = "test-secret";
    private readonly HttpClient _client;

    public class Factory : WebApplicationFactory<Program>
    {
        private readonly string _db = Path.Combine(Path.GetTempPath(), $"webhook-test-{Guid.NewGuid():N}.db");
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Webhook:Secret"] = Secret,
                ["ConnectionStrings:Default"] = $"Data Source={_db}"
            }));
    }

    public WebhookEndpointTests(Factory f) => _client = f.CreateClient();

    private Task<HttpResponseMessage> Post(string body, string? signature)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/payments")
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (signature != null) req.Headers.Add("X-Signature", signature);
        return _client.SendAsync(req);
    }

    private static string Event(string id, string type, string payId) =>
        $"{{\"id\":\"{id}\",\"type\":\"{type}\",\"data\":{{\"paymentId\":\"{payId}\",\"amount\":49900,\"currency\":\"INR\"}}}}";

    [Fact]
    public async Task Bad_signature_returns_401()
    {
        var r = await Post(Event("evt_a", "payment.succeeded", "pay_a"), "deadbeef");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Valid_event_is_stored_and_duplicate_is_ignored()
    {
        var body = Event("evt_b", "payment.succeeded", "pay_b");
        var sig = WebhookSignature.Compute(Secret, body);

        Assert.Equal(HttpStatusCode.OK, (await Post(body, sig)).StatusCode);
        var dup = await Post(body, sig);
        Assert.Equal(HttpStatusCode.OK, dup.StatusCode);
        Assert.Contains("duplicate", await dup.Content.ReadAsStringAsync());

        var p = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/payments/pay_b");
        Assert.Equal("succeeded", p.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Later_event_updates_payment_status()
    {
        var b1 = Event("evt_c1", "payment.succeeded", "pay_c");
        var b2 = Event("evt_c2", "payment.refunded", "pay_c");
        await Post(b1, WebhookSignature.Compute(Secret, b1));
        await Post(b2, WebhookSignature.Compute(Secret, b2));
        var p = await _client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/payments/pay_c");
        Assert.Equal("refunded", p.GetProperty("status").GetString());
    }
}

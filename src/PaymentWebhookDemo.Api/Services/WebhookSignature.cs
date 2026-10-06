using System.Security.Cryptography;
using System.Text;

namespace PaymentWebhookDemo.Api.Services;

/// <summary>HMAC-SHA256 signature check over the raw request body (hex digest).</summary>
public static class WebhookSignature
{
    public static string Compute(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static bool IsValid(string secret, string payload, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;
        var expected = Encoding.UTF8.GetBytes(Compute(secret, payload));
        var actual = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, actual); // constant-time
    }
}

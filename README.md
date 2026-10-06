# Payment Webhook Demo (ASP.NET Core Web API)

A small, self-contained example of how I build **secure, idempotent payment webhook receivers** in ASP.NET Core.
It is an original demo project: it uses a generic, made-up event format and contains no client or employer code or data.

## What it shows
- **HMAC-SHA256 signature verification** over the raw request body, with a constant-time comparison (`X-Signature` header)
- **Idempotency**: provider retries are safe, because each event id is stored once (unique index) and duplicates are acknowledged without re-processing
- Payment status updates (`succeeded`, `failed`, `refunded`), including later events overriding earlier ones
- Unknown event types are acknowledged (HTTP 200) so the provider doesn't keep retrying
- EF Core + SQLite (zero setup); swap `UseSqlite` for `UseSqlServer` to run on SQL Server
- Swagger UI, xUnit unit + integration tests (`WebApplicationFactory`)

## Run
```bash
dotnet run --project src/PaymentWebhookDemo.Api
# Swagger: http://localhost:5000/swagger (see console for the actual port)
```

Send a test event (replace `<sig>` with the HMAC-SHA256 hex of the exact body using the secret in `appsettings.json`):
```bash
curl -X POST http://localhost:5000/api/webhooks/payments \
  -H "Content-Type: application/json" -H "X-Signature: <sig>" \
  -d '{"id":"evt_1","type":"payment.succeeded","data":{"paymentId":"pay_1","amount":49900,"currency":"INR"}}'
```
Then `GET /api/payments/pay_1`.

## Test
```bash
dotnet test
```

## Notes
The secret in `appsettings.json` is a placeholder. In real projects, use user-secrets or environment variables, never commit real keys.
